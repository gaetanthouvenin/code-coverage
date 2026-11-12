// ------------------------------------------------------------------------------------------------
// <copyright file="RecurringJobSchedulerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

// The scheduler of the recurring jobs. The tests that need a database run on both engines and are
// skipped, with the reason, when Docker is not available.
[Collection(DatabaseCollection.Name)]
public sealed class RecurringJobSchedulerTests(DatabaseFixture database)
{
  #region Fields

  private const string JobType = "Tick";

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 32, 15, TimeSpan.Zero);

  private static readonly RecurringJobDefinition Sweep = new(JobType, TimeSpan.FromMinutes(5), "tick", "tick");

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Schedule_FromTwoReplicas_ShouldInsertOnce(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var first = BuildServices(provider, connectionString);
    await using var second = BuildServices(provider, connectionString);

    // Both replicas tick at the same time, then each one ticks again: the deduplication key alone keeps one job.
    var scheduled = await Task.WhenAll(Scheduler(first).ScheduleAsync(Ct), Scheduler(second).ScheduleAsync(Ct));
    (await Scheduler(first).ScheduleAsync(Ct)).ShouldBe(Definitions);
    (await Scheduler(second).ScheduleAsync(Ct)).ShouldBe(Definitions);

    scheduled.ShouldAllBe(static count => count == Definitions);
    var jobs = await JobsAsync(provider, connectionString);
    // One job per definition, whatever the number of replicas and of ticks.
    jobs.Count.ShouldBe(Definitions);
    jobs.Select(static j => j.DedupKey).ShouldBeUnique();
    var job = jobs.Where(static j => j.Type == JobType).ShouldHaveSingleItem();
    job.Type.ShouldBe(JobType);
    job.State.ShouldBe(JobState.Queued);
    job.OrganizationId.ShouldBeNull();
    job.DedupKey.ShouldBe("i:tick");
    job.LockKey.ShouldBe("i:tick");
    job.PayloadJson.ShouldBe(RecurringJobDefinition.EmptyPayload);
    job.RunAfter.ShouldBe(new DateTimeOffset(2026, 11, 8, 10, 35, 0, TimeSpan.Zero));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Schedule_WhileTheJobRuns_ShouldQueueTheNextSlot(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString);
    await Scheduler(services).ScheduleAsync(Ct);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.Jobs.ExecuteUpdateAsync(static s => s.SetProperty(j => j.State, JobState.Running).SetProperty(j => j.LockedBy, "worker-1"), Ct);
    }

    (await Scheduler(services).ScheduleAsync(Ct)).ShouldBe(Definitions);

    var jobs = (await JobsAsync(provider, connectionString)).Where(static j => j.Type == JobType).ToList();
    jobs.Count.ShouldBe(2);
    jobs.Count(static j => j.State == JobState.Queued).ShouldBe(1);
  }

  [Fact]
  public async Task Schedule_ShouldQueueACoalescedRequestForTheNextSlot()
  {
    var queue = A.Fake<IJobQueue>();
    await using var services = FakeServices(queue);
    using var scheduler = CreateScheduler(services, [Sweep]);

    (await scheduler.ScheduleAsync(Ct)).ShouldBe(1);

    A.CallTo(() => queue.EnqueueAsync(
                     A<JobRequest>.That.Matches(r => r.Type == JobType
                                                     && r.IsInstance
                                                     && r.DedupKey == "tick"
                                                     && r.LockKey == "tick"
                                                     && r.Coalesce
                                                     && r.MaxDelay == null
                                                     && r.RunAfter == new DateTimeOffset(2026, 11, 8, 10, 35, 0, TimeSpan.Zero)),
                     A<CancellationToken>._
                   ))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Schedule_WhenADefinitionFails_ShouldScheduleTheOthers()
  {
    var queue = A.Fake<IJobQueue>();
    A.CallTo(() => queue.EnqueueAsync(A<JobRequest>.That.Matches(static r => r.Type == JobType), A<CancellationToken>._))
     .Throws(new InvalidOperationException("The database is down."));
    await using var services = FakeServices(queue);
    using var scheduler = CreateScheduler(services, [Sweep, new RecurringJobDefinition("PurgeExpiredData", TimeSpan.FromDays(1), "purge", "purge")]);

    (await scheduler.ScheduleAsync(Ct)).ShouldBe(1);

    A.CallTo(() => queue.EnqueueAsync(A<JobRequest>.That.Matches(static r => r.Type == "PurgeExpiredData"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Execute_WhenDisabled_ShouldNeverTouchTheQueue()
  {
    var scopeFactory = A.Fake<IServiceScopeFactory>();
    using var scheduler = new RecurringJobScheduler(
      [Sweep],
      scopeFactory,
      Options.Create(new WorkerOptions { Enabled = false }),
      TimeProvider.System,
      NullLogger<RecurringJobScheduler>.Instance
    );

    await scheduler.StartAsync(Ct);
    await scheduler.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), Ct);

    scheduler.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
    A.CallTo(scopeFactory).MustNotHaveHappened();
  }

  [Fact]
  public async Task Execute_WithoutDefinition_ShouldStayIdle()
  {
    var scopeFactory = A.Fake<IServiceScopeFactory>();
    using var scheduler = new RecurringJobScheduler(
      [],
      scopeFactory,
      Options.Create(new WorkerOptions()),
      TimeProvider.System,
      NullLogger<RecurringJobScheduler>.Instance
    );

    await scheduler.StartAsync(Ct);
    await scheduler.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), Ct);

    scheduler.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
    A.CallTo(scopeFactory).MustNotHaveHappened();
  }

  [Fact]
  public void Ctor_WithDuplicateDedupKey_ShouldThrowArgumentException()
  {
    RecurringJobDefinition[] definitions = [Sweep, new RecurringJobDefinition("Other", TimeSpan.FromHours(1), "tick")];

    Should.Throw<ArgumentException>(() => new RecurringJobScheduler(
                                            definitions,
                                            A.Fake<IServiceScopeFactory>(),
                                            Options.Create(new WorkerOptions()),
                                            TimeProvider.System,
                                            NullLogger<RecurringJobScheduler>.Instance
                                          ))
          .Message.ShouldContain("'tick'");
  }

  [Fact]
  public void Catalog_ShouldHaveUniqueDedupKeys() =>
    RecurringJobCatalog.All.Select(static d => d.DedupKey).ShouldBeUnique();

  [Fact]
  public void Catalog_ShouldPurgeDaily()
  {
    var purge = RecurringJobCatalog.All.Single(static d => d.Type == JobTypes.PurgeExpiredData);

    purge.Interval.ShouldBe(TimeSpan.FromDays(1));
    purge.DedupKey.ShouldBe(PurgeExpiredDataJobs.Key);
  }

  [Theory]
  [InlineData("2026-11-08T10:32:15Z", 5, "2026-11-08T10:35:00Z")]
  [InlineData("2026-11-08T10:35:00Z", 5, "2026-11-08T10:40:00Z")]
  [InlineData("2026-11-08T10:32:15Z", 1440, "2026-11-09T00:00:00Z")]
  [InlineData("2026-11-08T23:59:59+02:00", 1440, "2026-11-09T00:00:00Z")]
  public void NextRun_ShouldReturnTheNextSlotBoundaryInUtc(string now, int intervalMinutes, string expected)
  {
    var definition = new RecurringJobDefinition(JobType, TimeSpan.FromMinutes(intervalMinutes), "sweep");

    definition.NextRun(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture))
              .ShouldBe(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
  }

  [Fact]
  public void Definition_WithIntervalBelowOneMinute_ShouldThrowArgumentOutOfRangeException() =>
    Should.Throw<ArgumentOutOfRangeException>(() => new RecurringJobDefinition(JobType, TimeSpan.FromSeconds(30), "sweep"));

  [Fact]
  public void Definition_WithoutDedupKey_ShouldThrow() =>
    Should.Throw<ArgumentException>(() => new RecurringJobDefinition(JobType, TimeSpan.FromMinutes(5), " "));

  private static IClock FixedClock()
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    return clock;
  }

  private static ServiceProvider FakeServices(IJobQueue queue) =>
    new ServiceCollection()
      .AddSingleton(FixedClock())
      .AddScoped(_ => queue)
      .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

  // What a replica schedules: the catalog of the application, and the sweep definition of these tests.
  private static int Definitions => RecurringJobCatalog.All.Count + 1;

  private static RecurringJobScheduler CreateScheduler(IServiceProvider services, RecurringJobDefinition[] definitions) =>
    new(
      definitions,
      services.GetRequiredService<IServiceScopeFactory>(),
      Options.Create(new WorkerOptions()),
      TimeProvider.System,
      NullLogger<RecurringJobScheduler>.Instance
    );

  // One replica of the Api: its own container, on the shared database, with the sweep definition.
  private static ServiceProvider BuildServices(DatabaseProvider provider, string connectionString)
  {
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                          ["ConnectionStrings:CodeCoverage"] = connectionString,
                          ["CodeCoverage:Database:Provider"] = provider.ToString(),
                        })
                        .Build();

    return new ServiceCollection()
           .AddSingleton<IConfiguration>(configuration)
           .AddSingleton(FixedClock())
           .AddLogging()
           .AddApplication()
           .AddInfrastructure()
           .AddPostgreSqlProvider()
           .AddSqlServerProvider()
           .AddJobProcessing()
           .AddSingleton(Sweep)
           .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
  }

  private static RecurringJobScheduler Scheduler(IServiceProvider services) =>
    services.GetServices<IHostedService>().OfType<RecurringJobScheduler>().Single();

  private static async Task<List<JobRecord>> JobsAsync(DatabaseProvider provider, string connectionString)
  {
    await using var check = DatabaseFixture.CreateContext(provider, connectionString);

    return await check.Jobs.AsNoTracking().OrderBy(static j => j.Id).ToListAsync(Ct);
  }

  private async Task<string> MigratedDatabaseAsync(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    return connectionString;
  }

  #endregion
}
