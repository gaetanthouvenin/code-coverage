// ------------------------------------------------------------------------------------------------
// <copyright file="SqlJobQueueTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Configurations;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

[Collection(DatabaseCollection.Name)]
public sealed class SqlJobQueueTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  // Never opened: these tests fail before the connection is needed.
  private const string UnusedConnection = "Host=localhost;Database=unused";

  private const string FinalizeType = "FinalizeCommit";

  private const string NotifyType = "NotifyCommit";

  private static readonly DateTimeOffset Start = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  private readonly TestMeterFactory _meterFactory = new();

  private readonly JobWakeUpSignal _wakeUpSignal = new(TimeProvider.System);

  private DateTimeOffset _now = Start;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_WithSameDedupKeyAndCoalesce_ShouldKeepOneJob(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);

    var first = await queue.EnqueueAsync(Finalize("""{"commitId":42}"""), Ct);
    _now = Start.AddSeconds(30);
    var second = await queue.EnqueueAsync(Finalize("""{"commitId":42,"late":true}"""), Ct);

    second.ShouldBe(first);
    var job = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    job.Id.ShouldBe(first);
    job.State.ShouldBe(JobState.Queued);
    job.PayloadJson.ShouldBe("""{"commitId":42}""");
    job.RunAfter.ShouldBe(Start);
    job.FirstQueuedAt.ShouldBe(Start);
    job.MaxAttempts.ShouldBe(8);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_WithDebounce_ShouldPushRunAfterUpToMaxDelay(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);

    var id = await queue.EnqueueAsync(Notify(), Ct);
    (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem().RunAfter.ShouldBe(Start.AddSeconds(20));

    // Within the window: the twin is pushed by the debounce.
    _now = Start.AddSeconds(100);
    (await queue.EnqueueAsync(Notify(), Ct)).ShouldBe(id);
    (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem().RunAfter.ShouldBe(Start.AddSeconds(120));

    // Past the maximum delay since the first queuing: capped.
    _now = Start.AddSeconds(290);
    (await queue.EnqueueAsync(Notify(), Ct)).ShouldBe(id);

    var job = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    job.RunAfter.ShouldBe(Start.AddSeconds(300));
    job.FirstQueuedAt.ShouldBe(Start);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_WhileTwinRunning_ShouldQueueNewJob(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    var running = await queue.EnqueueAsync(Finalize("""{"commitId":42}"""), Ct);
    await context.Jobs
                 .Where(j => j.Id == running)
                 .ExecuteUpdateAsync(static s => s.SetProperty(j => j.State, JobState.Running).SetProperty(j => j.LockedBy, "worker-1"), Ct);

    var queued = await queue.EnqueueAsync(Finalize("""{"commitId":42}"""), Ct);

    queued.ShouldNotBe(running);
    var jobs = await JobsAsync(provider, connectionString);
    jobs.Count.ShouldBe(2);
    jobs.Single(j => j.Id == running).State.ShouldBe(JobState.Running);
    jobs.Single(j => j.Id == queued).State.ShouldBe(JobState.Queued);

    // The new job is now the queued twin: the next request is merged into it.
    (await queue.EnqueueAsync(Finalize("""{"commitId":42}"""), Ct)).ShouldBe(queued);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_WithoutDedupKey_ShouldQueueEachRequest(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context, maxAttempts: "5");
    var request = JobRequest.ForInstance("ProcessUpload", """{"uploadId":7}""");

    var first = await queue.EnqueueAsync(request, Ct);
    var second = await queue.EnqueueAsync(request, Ct);

    second.ShouldNotBe(first);
    var jobs = await JobsAsync(provider, connectionString);
    jobs.Count.ShouldBe(2);
    jobs.ShouldAllBe(j => j.State == JobState.Queued
                          && j.Attempts == 0
                          && j.MaxAttempts == 5
                          && j.LockKey == null
                          && j.DedupKey == null
                          && j.CreatedAt == Start
                          && j.CorrelationId.Length > 0);
    context.ChangeTracker.Entries().ShouldBeEmpty();
  }

  // Once committed, the job wakes an idle loop of the worker of this process.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_AfterCommit_ShouldWakeUpTheWorker(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);

    await queue.EnqueueAsync(Finalize("{}"), Ct);

    (await _wakeUpSignal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeTrue();
  }

  // The job is part of the transaction of the caller: rolled back with it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task EnqueueInTransaction_WhenCallerRollsBack_ShouldQueueNothing(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await using var transaction = await context.Database.BeginTransactionAsync(Ct);
      await CreateQueue(context).EnqueueInTransactionAsync(context, Finalize("{}"), Ct);
      await transaction.RollbackAsync(Ct);
    }

    (await JobsAsync(provider, connectionString)).ShouldBeEmpty();
  }

  // The twin is inserted by a transaction not committed yet: the second request waits on the unique
  // index (or on the row), then is merged into the twin instead of failing.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_ConcurrentlyWithSameDedupKey_ShouldMergeIntoTheTwin(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var first = DatabaseFixture.CreateContext(provider, connectionString);
    await using var second = DatabaseFixture.CreateContext(provider, connectionString);

    await using var transaction = await first.Database.BeginTransactionAsync(Ct);
    var twin = await CreateQueue(first).EnqueueInTransactionAsync(first, Finalize("{}"), Ct);

    var concurrent = CreateQueue(second).EnqueueAsync(Finalize("{}"), Ct);
    await Task.Delay(TimeSpan.FromSeconds(1), Ct);
    concurrent.IsCompleted.ShouldBeFalse("the second request waits for the first transaction");
    await transaction.CommitAsync(Ct);

    (await concurrent.WaitAsync(TimeSpan.FromSeconds(30), Ct)).ShouldBe(twin);
    (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem().Id.ShouldBe(twin);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Insert_SecondQueuedJobWithSameDedupKey_ShouldBeRefusedByTheIndex(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    context.Jobs.Add(Record(JobState.Queued, dedupKey: "finalize:42", lockKey: null));
    context.Jobs.Add(Record(JobState.Running, dedupKey: "finalize:42", lockKey: null));
    context.Jobs.Add(Record(JobState.Queued, dedupKey: null, lockKey: null));
    context.Jobs.Add(Record(JobState.Queued, dedupKey: null, lockKey: null));
    await context.SaveChangesAsync(Ct);
    context.ChangeTracker.Clear();

    context.Jobs.Add(Record(JobState.Queued, dedupKey: "finalize:42", lockKey: null));
    var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

    UniqueIndexViolation.Is(exception, JobRecordConfiguration.DedupIndexName).ShouldBeTrue();
    UniqueIndexViolation.Is(exception, JobRecordConfiguration.LockIndexName).ShouldBeFalse();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Insert_SecondRunningJobWithSameLockKey_ShouldBeRefusedByTheIndex(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    context.Jobs.Add(Record(JobState.Running, dedupKey: null, lockKey: "commit:42"));
    context.Jobs.Add(Record(JobState.Queued, dedupKey: null, lockKey: "commit:42"));
    context.Jobs.Add(Record(JobState.Queued, dedupKey: null, lockKey: "commit:42"));
    context.Jobs.Add(Record(JobState.Running, dedupKey: null, lockKey: null));
    context.Jobs.Add(Record(JobState.Running, dedupKey: null, lockKey: null));
    await context.SaveChangesAsync(Ct);
    context.ChangeTracker.Clear();

    context.Jobs.Add(Record(JobState.Running, dedupKey: null, lockKey: "commit:42"));
    var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

    UniqueIndexViolation.Is(exception, JobRecordConfiguration.LockIndexName).ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Fail_OnLastAttempt_ShouldCountTheDeadJob(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    using var recorder = new MeasurementRecorder(_meterFactory);
    var exhausted = Record(JobState.Running, dedupKey: null, lockKey: null);
    exhausted.LockedBy = "worker-1";
    exhausted.LockedUntil = Start.AddMinutes(5);
    exhausted.Attempts = exhausted.MaxAttempts;
    var retried = Record(JobState.Running, dedupKey: null, lockKey: null);
    retried.LockedBy = "worker-1";
    retried.LockedUntil = Start.AddMinutes(5);
    retried.Attempts = 1;

    await using (var seed = DatabaseFixture.CreateContext(provider, connectionString))
    {
      seed.Jobs.AddRange(exhausted, retried);
      await seed.SaveChangesAsync(Ct);
    }

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);
    (await queue.FailAsync(exhausted.Id, "worker-1", "boom", Start.AddMinutes(1), Ct)).ShouldBeTrue();
    (await queue.FailAsync(retried.Id, "worker-1", "boom", Start.AddMinutes(1), Ct)).ShouldBeTrue();

    var jobs = await JobsAsync(provider, connectionString);
    jobs.Single(j => j.Id == exhausted.Id).State.ShouldBe(JobState.Dead);
    jobs.Single(j => j.Id == retried.Id).State.ShouldBe(JobState.Queued);
    recorder.Sum(CodeCoverageMeter.JobsDeadTotal).ShouldBe(1, "only the job that used all its attempts is dead");
  }

  // A debounced job waits for its time, not for a worker: only the jobs that may run age.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Measure_ShouldCountQueuedJobsByTypeAndAgeOfOldestDueJob(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var oldest = Record(JobState.Queued, dedupKey: null, lockKey: null);
    oldest.RunAfter = Start.AddMinutes(-3);
    var due = Record(JobState.Queued, dedupKey: null, lockKey: null);
    due.RunAfter = Start.AddMinutes(-1);
    var later = Record(JobState.Queued, dedupKey: null, lockKey: null);
    later.Type = NotifyType;
    later.RunAfter = Start.AddMinutes(10);
    var running = Record(JobState.Running, dedupKey: null, lockKey: null);
    running.RunAfter = Start.AddHours(-1);

    await using (var seed = DatabaseFixture.CreateContext(provider, connectionString))
    {
      seed.Jobs.AddRange(oldest, due, later, running);
      await seed.SaveChangesAsync(Ct);
    }

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var snapshot = await CreateQueue(context).MeasureAsync(Ct);

    snapshot.QueuedByType.Count.ShouldBe(2);
    snapshot.QueuedByType[FinalizeType].ShouldBe(2);
    snapshot.QueuedByType[NotifyType].ShouldBe(1);
    snapshot.OldestDueAgeSeconds.ShouldBe(180, tolerance: 0.001);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Insert_Default_ShouldStampCurrentOrganization(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var organization = new OrganizationContext();
    organization.Enter(7);
    var queue = CreateQueue(context, organization: organization);

    await queue.EnqueueAsync(JobRequest.Create(FinalizeType, "{}", lockKey: "commit:42", dedupKey: "finalize:42"), Ct);

    var job = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    job.OrganizationId.ShouldBe(7);
    job.LockKey.ShouldBe("o7:commit:42");
    job.DedupKey.ShouldBe("o7:finalize:42");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Insert_DefaultWithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);

    await Should.ThrowAsync<InvalidOperationException>(() => queue.EnqueueAsync(JobRequest.Create(FinalizeType, "{}"), Ct));

    (await JobsAsync(provider, connectionString)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Insert_ForInstance_ShouldStoreNoOrganizationAndTheInstancePrefix(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);

    await CreateQueue(context).EnqueueAsync(Finalize("{}"), Ct);

    var job = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    job.OrganizationId.ShouldBeNull();
    job.LockKey.ShouldBe("i:commit:42");
    job.DedupKey.ShouldBe("i:finalize:42");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_ForOrganizationInsideAnotherOne_ShouldThrow(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var organization = new OrganizationContext();
    organization.Enter(7);
    var queue = CreateQueue(context, organization: organization);

    await Should.ThrowAsync<InvalidOperationException>(
      () => queue.EnqueueAsync(JobRequest.ForOrganization(8, FinalizeType, "{}"), Ct)
    );

    (await JobsAsync(provider, connectionString)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_SameDedupKeyOtherOrganization_ShouldNotMerge(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = CreateQueue(context);

    var first = await queue.EnqueueAsync(FinalizeFor(1), Ct);
    var other = await queue.EnqueueAsync(FinalizeFor(2), Ct);
    var instance = await queue.EnqueueAsync(Finalize("{}"), Ct);
    var again = await queue.EnqueueAsync(FinalizeFor(1), Ct);

    new[] { first, other, instance }.ShouldBeUnique();
    again.ShouldBe(first, "the same organization still merges");
    (await JobsAsync(provider, connectionString)).Select(static j => j.DedupKey)
                                                 .ShouldBe(["o1:finalize:42", "o2:finalize:42", "i:finalize:42"], ignoreOrder: true);
  }

  // The longest prefix and the longest key together: the real column has to take them on both engines.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Enqueue_LongestKey_ShouldFitTheColumn(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var key = new string('k', JobRequest.KeyMaxLength);
    var expected = $"o{long.MaxValue}:{key}";

    await CreateQueue(context).EnqueueAsync(JobRequest.ForOrganization(long.MaxValue, FinalizeType, "{}", key, key), Ct);

    expected.Length.ShouldBeLessThanOrEqualTo(JobRecord.KeyMaxLength);
    var job = (await JobsAsync(provider, connectionString)).ShouldHaveSingleItem();
    job.LockKey.ShouldBe(expected);
    job.DedupKey.ShouldBe(expected);
  }

  [Fact]
  public async Task EnqueueInTransaction_WithoutTransaction_ShouldThrowInvalidOperationException()
  {
    await using var context = DatabaseFixture.CreateContext(DatabaseProvider.PostgreSql, UnusedConnection);

    await Should.ThrowAsync<InvalidOperationException>(
      () => CreateQueue(context).EnqueueInTransactionAsync(context, Finalize("{}"), Ct)
    );
  }

  [Fact]
  public async Task Enqueue_WithNullArguments_ShouldThrowArgumentNullException()
  {
    await using var context = DatabaseFixture.CreateContext(DatabaseProvider.PostgreSql, UnusedConnection);
    var queue = CreateQueue(context);

    await Should.ThrowAsync<ArgumentNullException>(() => queue.EnqueueAsync(null!, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => queue.EnqueueInTransactionAsync(null!, Finalize("{}"), Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => queue.EnqueueInTransactionAsync(context, null!, Ct));
  }

  private static JobRequest Finalize(string payloadJson) =>
    JobRequest.ForInstance(FinalizeType, payloadJson, lockKey: "commit:42", dedupKey: "finalize:42", coalesce: true);

  private static JobRequest FinalizeFor(long organizationId) =>
    JobRequest.ForOrganization(organizationId, FinalizeType, "{}", dedupKey: "finalize:42", coalesce: true);

  private static JobRecord Record(JobState state, string? dedupKey, string? lockKey) =>
    new()
    {
      Type = FinalizeType,
      PayloadJson = "{}",
      State = state,
      DedupKey = dedupKey,
      LockKey = lockKey,
      MaxAttempts = 8,
      RunAfter = Start,
      FirstQueuedAt = Start,
      CorrelationId = "test",
      CreatedAt = Start,
    };

  private static async Task<List<JobRecord>> JobsAsync(DatabaseProvider provider, string connectionString)
  {
    await using var check = DatabaseFixture.CreateContext(provider, connectionString);

    return await check.Jobs.AsNoTracking().OrderBy(static j => j.Id).ToListAsync(Ct);
  }

  private JobRequest Notify() =>
    JobRequest.ForInstance(
      NotifyType,
      """{"commitId":42,"force":false}""",
      lockKey: "commit:42",
      dedupKey: "notify:42",
      runAfter: _now.AddSeconds(20),
      maxDelay: TimeSpan.FromSeconds(300)
    );

  private SqlJobQueue CreateQueue(
    CodeCoverageDbContext context,
    string? maxAttempts = null,
    ICurrentOrganization? organization = null)
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).ReturnsLazily(() => _now);
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?> { ["Processing:JobMaxAttempts"] = maxAttempts })
                        .Build();

    return new SqlJobQueue(
      context,
      organization ?? new OrganizationContext(),
      clock,
      configuration,
      JobClaimDialects.For(context),
      _wakeUpSignal,
      new CodeCoverageMeter(_meterFactory)
    );
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
