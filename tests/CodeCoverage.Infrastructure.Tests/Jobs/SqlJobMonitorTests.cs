// ------------------------------------------------------------------------------------------------
// <copyright file="SqlJobMonitorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

// The read of the queue for the administrators and the retry of a job, on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class SqlJobMonitorTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly TestMeterFactory _meterFactory = new();

  private readonly JobWakeUpSignal _wakeUpSignal = new(TimeProvider.System);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetStatus_ShouldReportOldestQueuedAge(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await AddAsync(provider, connectionString,
      Record(JobState.Queued, Now.AddMinutes(-10)),
      Record(JobState.Queued, Now.AddMinutes(-3)),
      Record(JobState.Queued, Now.AddMinutes(5)),
      Record(JobState.Running, Now.AddMinutes(-30)),
      Record(JobState.Dead, Now.AddMinutes(-60)),
      Record(JobState.Failed, Now.AddMinutes(-60)),
      Record(JobState.Succeeded, Now.AddMinutes(-60)));
    var pulse = new WorkerPulse();
    pulse.Beat(Now.AddSeconds(-2));
    var meter = new CodeCoverageMeter(_meterFactory);
    meter.ReportGitHubRateLimit("42", 4321);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var status = await new SqlJobMonitor(context, pulse, meter, Clock()).GetStatusAsync(Ct);

    // The job not due yet does not count in the age.
    status.OldestQueuedAge.ShouldBe(TimeSpan.FromMinutes(10));
    (status.Queued, status.Running, status.Failed, status.Dead).ShouldBe((3, 1, 1, 1));
    status.WorkerLastSeenAt.ShouldBe(Now.AddSeconds(-2));
    status.GitHubRemaining.ShouldBe(new Dictionary<string, long> { ["42"] = 4321 });
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task List_ShouldPageTheJobsOfEveryLaneTheMostRecentFirst(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await AddAsync(provider, connectionString, Record(JobState.Dead, Now), Record(JobState.Queued, Now), Record(JobState.Dead, Now));

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var monitor = new SqlJobMonitor(context, new WorkerPulse(), new CodeCoverageMeter(_meterFactory), Clock());

    var dead = await monitor.ListAsync(JobState.Dead, 1, 1, Ct);
    var all = await monitor.ListAsync(null, 1, 10, Ct);

    dead.TotalCount.ShouldBe(2);
    dead.Items.ShouldHaveSingleItem().Id.ShouldBe(all.Items[0].Id);
    all.Items.Select(static j => j.Id).ShouldBeInOrder(SortDirection.Descending);
    all.Items.ShouldAllBe(j => j.HasError);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Retry_DeadJob_ShouldResetTheRowInPlace(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var dead = Record(JobState.Dead, Now.AddHours(-1));
    dead.Attempts = 8;
    dead.OrganizationId = null;
    await AddAsync(provider, connectionString, dead);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      (await Queue(context).RetryAsync(dead.Id, Ct)).ShouldBe(JobRetryResult.Retried);
    }

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    var row = await check.Jobs.AsNoTracking().SingleAsync(j => j.Id == dead.Id, Ct);

    row.State.ShouldBe(JobState.Queued);
    row.Attempts.ShouldBe(0);
    row.RunAfter.ShouldBe(Now);
    row.LockedBy.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Retry_RunningUnknownOrTwinWaiting_ShouldRefuse(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var running = Record(JobState.Running, Now);
    var twin = Record(JobState.Queued, Now);
    twin.DedupKey = "finalize:7";
    var failed = Record(JobState.Failed, Now);
    failed.DedupKey = "finalize:7";
    await AddAsync(provider, connectionString, running, twin, failed);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var queue = Queue(context);

    (await queue.RetryAsync(running.Id, Ct)).ShouldBe(JobRetryResult.NotRetryable);
    (await queue.RetryAsync(failed.Id, Ct)).ShouldBe(JobRetryResult.NotRetryable);
    (await queue.RetryAsync(999_999, Ct)).ShouldBe(JobRetryResult.NotFound);
  }

  public void Dispose() => _meterFactory.Dispose();

  private static IClock Clock()
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    return clock;
  }

  private static JobRecord Record(JobState state, DateTimeOffset runAfter) =>
    new()
    {
      Type = "FinalizeCommit",
      PayloadJson = """{"secret":"payload"}""",
      State = state,
      MaxAttempts = 8,
      RunAfter = runAfter,
      FirstQueuedAt = runAfter,
      LastError = "boom",
      CorrelationId = "test",
      CreatedAt = runAfter,
    };

  private static async Task AddAsync(DatabaseProvider provider, string connectionString, params JobRecord[] jobs)
  {
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);

    context.Jobs.AddRange(jobs);
    await context.SaveChangesAsync(Ct);
  }

  private SqlJobQueue Queue(CodeCoverageDbContext context) =>
    new(context, new OrganizationContext(), Clock(), new ConfigurationBuilder().Build(), JobClaimDialects.For(context), _wakeUpSignal, new CodeCoverageMeter(_meterFactory));

  private async Task<string> MigratedDatabaseAsync(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    return connectionString;
  }

  #endregion
}
