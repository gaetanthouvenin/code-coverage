// ------------------------------------------------------------------------------------------------
// <copyright file="JobClaimConcurrencyTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

// The claim of the jobs (SKIP LOCKED / READPAST) and the writes of the worker that holds them, on
// both engines. Skipped, with the reason, when Docker is not available.
[Collection(DatabaseCollection.Name)]
public sealed class JobClaimConcurrencyTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  private const int WorkerCount = 8;

  private const string JobType = "ProcessUpload";

  private static readonly DateTimeOffset Start = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  private readonly TestMeterFactory _meterFactory = new();

  private DateTimeOffset _now = Start.AddSeconds(1);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Claim_With8ConcurrentWorkers_ShouldNeverRunSameJobTwice(DatabaseProvider provider)
  {
    const int jobCount = 1000;
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, Enumerable.Range(0, jobCount).Select(_ => Record()));
    var claims = new ConcurrentBag<(long JobId, string Worker)>();

    await RunWorkersAsync(
      provider,
      connectionString,
      async (queue, worker) =>
      {
        // A worker stops after several empty claims in a row, not on the first one: on SQL Server a
        // claim that loses its deadlock rounds returns nothing while jobs are still queued.
        var idleRounds = 0;

        while (claims.Count < jobCount && idleRounds < 50)
        {
          if (await queue.ClaimNextAsync(worker, Ct) is not { } job)
          {
            idleRounds++;
            await Task.Delay(10, Ct);

            continue;
          }

          idleRounds = 0;
          claims.Add((job.Id, worker));
          (await queue.CompleteAsync(job.Id, worker, Ct)).ShouldBeTrue();
        }
      }
    );

    claims.Count.ShouldBe(jobCount);
    claims.Select(static c => c.JobId).Distinct().Count().ShouldBe(jobCount, "no job is claimed twice");
    var jobs = await JobsAsync(provider, connectionString);
    jobs.Count.ShouldBe(jobCount);
    jobs.ShouldAllBe(j => j.State == JobState.Succeeded && j.Attempts == 1 && j.LockedUntil == null && j.CompletedAt == _now);
    jobs.Select(static j => j.LockedBy).Distinct().Count().ShouldBeGreaterThan(1, "the workers share the jobs");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Claim_WithSameLockKey_ShouldRunOneAtATime(DatabaseProvider provider)
  {
    const int jobCount = 30;
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, Enumerable.Range(0, jobCount).Select(_ => Record(lockKey: "commit:7")));
    var running = 0;
    var maxRunning = 0;
    var completed = new ConcurrentBag<long>();

    await RunWorkersAsync(
      provider,
      connectionString,
      async (queue, worker) =>
      {
        // A worker that finds nothing to claim retries for a while: the key is held by another one.
        var idleRounds = 0;

        while (completed.Count < jobCount && idleRounds < 200)
        {
          if (await queue.ClaimNextAsync(worker, Ct) is not { } job)
          {
            idleRounds++;
            await Task.Delay(10, Ct);

            continue;
          }

          idleRounds = 0;
          var current = Interlocked.Increment(ref running);
          InterlockedMax(ref maxRunning, current);
          await Task.Delay(5, Ct);

          // Released before the completion: the next job of the key may be claimed right after it.
          Interlocked.Decrement(ref running);
          (await queue.CompleteAsync(job.Id, worker, Ct)).ShouldBeTrue();
          completed.Add(job.Id);
        }
      }
    );

    maxRunning.ShouldBe(1, "the jobs of one LockKey never run at the same time");
    completed.Distinct().Count().ShouldBe(jobCount);
    (await JobsAsync(provider, connectionString)).ShouldAllBe(j => j.State == JobState.Succeeded);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Claim_WhileJobOfSameKeyRuns_ShouldPassOverTheKey(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(
      provider,
      connectionString,
      [Record(lockKey: "commit:42"), Record(lockKey: "commit:42"), Record(runAfter: Start.AddMilliseconds(1))]
    );
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);

    var first = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();
    var other = (await queue.ClaimNextAsync("worker-2", Ct)).ShouldNotBeNull();
    (await queue.ClaimNextAsync("worker-3", Ct)).ShouldBeNull();

    first.LockKey.ShouldBe("commit:42");
    other.LockKey.ShouldBeNull();

    (await queue.CompleteAsync(first.Id, "worker-1", Ct)).ShouldBeTrue();
    var second = (await queue.ClaimNextAsync("worker-3", Ct)).ShouldNotBeNull();
    second.LockKey.ShouldBe("commit:42");
    second.Id.ShouldNotBe(first.Id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Complete_AfterLeaseLost_ShouldWriteNothing(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, [Record()]);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    var job = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();

    // The lease of worker-1 expired: the job was queued again, then claimed by worker-2.
    _now = Start.AddMinutes(6);
    await context.Jobs
                 .Where(j => j.Id == job.Id)
                 .ExecuteUpdateAsync(
                   static s => s.SetProperty(j => j.State, JobState.Queued)
                                .SetProperty(j => j.LockedBy, (string?)null)
                                .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null),
                   Ct
                 );
    (await queue.ClaimNextAsync("worker-2", Ct)).ShouldNotBeNull().Id.ShouldBe(job.Id);

    (await queue.CompleteAsync(job.Id, "worker-1", Ct)).ShouldBeFalse();
    (await queue.FailAsync(job.Id, "worker-1", "boom", retryAt: null, Ct)).ShouldBeFalse();
    (await queue.FailAsync(job.Id, "worker-1", "boom", retryAt: _now, Ct)).ShouldBeFalse();
    (await queue.ExtendLeaseAsync(job.Id, "worker-1", Ct)).ShouldBeFalse();

    var stored = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    stored.State.ShouldBe(JobState.Running);
    stored.LockedBy.ShouldBe("worker-2");
    stored.LockedUntil.ShouldBe(_now + SqlJobQueue.LeaseDuration);
    stored.Attempts.ShouldBe(2);
    stored.LastError.ShouldBeNull();
    stored.CompletedAt.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Claim_ShouldTakeTheDueJobsInRunAfterOrder(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    _now = Start.AddSeconds(20);
    await SeedAsync(
      provider,
      connectionString,
      [Record(runAfter: Start.AddSeconds(10)), Record(runAfter: Start), Record(runAfter: Start.AddHours(1))]
    );
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);

    var first = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();
    var second = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();
    (await queue.ClaimNextAsync("worker-1", Ct)).ShouldBeNull("the last job is not due yet");

    first.RunAfter.ShouldBe(Start);
    second.RunAfter.ShouldBe(Start.AddSeconds(10));
    first.State.ShouldBe(JobState.Running);
    first.LockedBy.ShouldBe("worker-1");
    first.LockedUntil.ShouldBe(_now + SqlJobQueue.LeaseDuration);
    first.Attempts.ShouldBe(1);
  }

  // A claim that races another one on the same key is refused by the Running index: the refusal is
  // absorbed. The race is forced by a dialect that leaves out the NOT EXISTS clause.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Claim_WhenRunningIndexRefuses_ShouldAbsorbTheViolation(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, [Record(lockKey: "commit:42"), Record(lockKey: "commit:42")]);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context, new KeyBlindJobClaimDialect(JobClaimDialects.For(provider)));
    var running = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();

    (await queue.ClaimNextAsync("worker-2", Ct)).ShouldBeNull();

    var jobs = await JobsAsync(provider, connectionString);
    jobs.Single(j => j.Id == running.Id).State.ShouldBe(JobState.Running);
    var waiting = jobs.Single(j => j.Id != running.Id);
    waiting.State.ShouldBe(JobState.Queued);
    waiting.Attempts.ShouldBe(0);
    waiting.LockedBy.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ExtendLease_WhileHeld_ShouldPushLockedUntil(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, [Record()]);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    var job = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();

    _now = Start.AddMinutes(4);
    (await queue.ExtendLeaseAsync(job.Id, "worker-1", Ct)).ShouldBeTrue();

    var stored = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    stored.State.ShouldBe(JobState.Running);
    stored.LockedUntil.ShouldBe(Start.AddMinutes(4) + SqlJobQueue.LeaseDuration);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Fail_WithoutRetry_ShouldMarkFailed(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, [Record()]);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    var job = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();

    (await queue.FailAsync(job.Id, "worker-1", "not found", retryAt: null, Ct)).ShouldBeTrue();

    var stored = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    stored.State.ShouldBe(JobState.Failed);
    stored.LastError.ShouldBe("not found");
    stored.LockedBy.ShouldBe("worker-1");
    stored.LockedUntil.ShouldBeNull();
    stored.CompletedAt.ShouldBe(_now);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Fail_WithRetry_ShouldQueueTheJobAgain(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, [Record()]);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    var job = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();
    var retryAt = _now.AddSeconds(20);

    (await queue.FailAsync(job.Id, "worker-1", "timeout", retryAt, Ct)).ShouldBeTrue();

    var stored = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    stored.State.ShouldBe(JobState.Queued);
    stored.RunAfter.ShouldBe(retryAt);
    stored.Attempts.ShouldBe(1);
    stored.LastError.ShouldBe("timeout");
    stored.LockedBy.ShouldBeNull();
    stored.LockedUntil.ShouldBeNull();
    stored.CompletedAt.ShouldBeNull();
    (await queue.ClaimNextAsync("worker-1", Ct)).ShouldBeNull("the retry is not due yet");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Fail_WithRetryAfterLastAttempt_ShouldMarkDead(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var record = Record();
    record.Attempts = record.MaxAttempts - 1;
    await SeedAsync(provider, connectionString, [record]);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    var job = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();

    (await queue.FailAsync(job.Id, "worker-1", "timeout", _now.AddSeconds(20), Ct)).ShouldBeTrue();

    var stored = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    stored.State.ShouldBe(JobState.Dead);
    stored.Attempts.ShouldBe(stored.MaxAttempts);
    stored.LastError.ShouldBe("timeout");
    stored.CompletedAt.ShouldBe(_now);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Fail_WithRetryAndQueuedTwin_ShouldAbsorbTheJob(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await SeedAsync(provider, connectionString, [Record(dedupKey: "finalize:42")]);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    var job = (await queue.ClaimNextAsync("worker-1", Ct)).ShouldNotBeNull();
    await SeedAsync(provider, connectionString, [Record(dedupKey: "finalize:42")]);

    (await queue.FailAsync(job.Id, "worker-1", "timeout", _now.AddSeconds(20), Ct)).ShouldBeTrue();

    var jobs = await JobsAsync(provider, connectionString);
    var absorbed = jobs.Single(j => j.Id == job.Id);
    absorbed.State.ShouldBe(JobState.Succeeded);
    absorbed.LastError.ShouldBe("timeout");
    absorbed.CompletedAt.ShouldBe(_now);
    jobs.Single(j => j.Id != job.Id).State.ShouldBe(JobState.Queued);
  }

  private static void InterlockedMax(ref int target, int value)
  {
    int current;

    do
    {
      current = Volatile.Read(ref target);

      if (value <= current)
      {
        return;
      }
    }
    while (Interlocked.CompareExchange(ref target, value, current) != current);
  }

  private static JobRecord Record(string? lockKey = null, string? dedupKey = null, DateTimeOffset? runAfter = null) =>
    new()
    {
      Type = JobType,
      PayloadJson = "{}",
      State = JobState.Queued,
      LockKey = lockKey,
      DedupKey = dedupKey,
      MaxAttempts = 8,
      RunAfter = runAfter ?? Start,
      FirstQueuedAt = Start,
      CorrelationId = "test",
      CreatedAt = Start,
    };

  private static async Task SeedAsync(DatabaseProvider provider, string connectionString, IEnumerable<JobRecord> records)
  {
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    context.Jobs.AddRange(records);
    await context.SaveChangesAsync(Ct);
  }

  private static async Task<List<JobRecord>> JobsAsync(DatabaseProvider provider, string connectionString)
  {
    await using var check = DatabaseFixture.CreateContext(provider, connectionString);

    return await check.Jobs.AsNoTracking().OrderBy(static j => j.Id).ToListAsync(Ct);
  }

  private SqlJobQueue CreateQueue(CodeCoverageDbContext context, IJobClaimDialect? dialect = null)
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).ReturnsLazily(() => _now);

    return new SqlJobQueue(
      context,
      clock,
      new ConfigurationBuilder().Build(),
      dialect ?? JobClaimDialects.For(context),
      new JobWakeUpSignal(TimeProvider.System),
      new CodeCoverageMeter(_meterFactory)
    );
  }

  // Each worker has its own context (one connection), as each worker loop of the Api has its own scope.
  private async Task RunWorkersAsync(
    DatabaseProvider provider,
    string connectionString,
    Func<SqlJobQueue, string, Task> work)
  {
    var workers = Enumerable.Range(1, WorkerCount)
                            .Select(i => Task.Run(
                                      async () =>
                                      {
                                        await using var context = DatabaseFixture.CreateContext(provider, connectionString);
                                        await work(CreateQueue(context), $"worker-{i}");
                                      },
                                      Ct
                                    ));

    await Task.WhenAll(workers).WaitAsync(TimeSpan.FromMinutes(5), Ct);
  }

  private async Task<string> MigratedDatabaseAsync(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    return connectionString;
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _meterFactory.Dispose();

  #endregion
}
