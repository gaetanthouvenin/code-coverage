// ------------------------------------------------------------------------------------------------
// <copyright file="JobWorkerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.Metrics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

// The worker and the lease sweeper, wired as in the Api, on both engines. The tests that need a
// database are skipped, with the reason, when Docker is not available.
[Collection(DatabaseCollection.Name)]
public sealed class JobWorkerTests(DatabaseFixture database)
{
  #region Fields

  private const string WorkerId = "worker-1";

  private const string JobType = "Test";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Run_WhenHandlerThrowsRetryLater_ShouldRescheduleWithoutConsumingAttempt(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var dispatcher = Throwing(new RetryLaterException(TimeSpan.FromMinutes(5), "The base commit has pending uploads."));
    await using var services = BuildServices(provider, connectionString, dispatcher);
    var id = await SeedAsync(provider, connectionString, Record());
    var before = DateTimeOffset.UtcNow;

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    var job = await JobAsync(provider, connectionString, id);
    job.State.ShouldBe(JobState.Queued);
    job.Attempts.ShouldBe(0, "waiting never consumes an attempt");
    job.RunAfter.ShouldBeGreaterThanOrEqualTo(before.AddMinutes(5).AddSeconds(-1));
    job.RunAfter.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5).AddSeconds(1));
    job.LockedBy.ShouldBeNull();
    job.LockedUntil.ShouldBeNull();
    job.LastError!.ShouldContain("The base commit has pending uploads.");
    job.CompletedAt.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Run_WhenNotFound_ShouldFailPermanently(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString, Throwing(NotFoundException.For("Commit", "42")));
    var id = await SeedAsync(provider, connectionString, Record());

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    var job = await JobAsync(provider, connectionString, id);
    job.State.ShouldBe(JobState.Failed);
    job.Attempts.ShouldBe(1);
    job.LastError.ShouldNotBeNullOrWhiteSpace();
    job.LockedUntil.ShouldBeNull();
    job.CompletedAt.ShouldNotBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Run_WhenHandlerFails_ShouldRetryAfterBackoff(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString, Throwing(new InvalidOperationException("GitHub is down")));
    var id = await SeedAsync(provider, connectionString, Record());
    var before = DateTimeOffset.UtcNow;

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    // First failure: 2^1 x 10 s = 20 s, spread by 20 %.
    var job = await JobAsync(provider, connectionString, id);
    job.State.ShouldBe(JobState.Queued);
    job.Attempts.ShouldBe(1);
    job.RunAfter.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(16));
    job.RunAfter.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(24));
    job.LastError.ShouldBe("InvalidOperationException: GitHub is down");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Run_AfterMaxAttempts_ShouldMarkDead(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString, Throwing(new InvalidOperationException("boom")));
    var record = Record();
    record.Attempts = record.MaxAttempts - 1;
    var id = await SeedAsync(provider, connectionString, record);

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    var job = await JobAsync(provider, connectionString, id);
    job.State.ShouldBe(JobState.Dead);
    job.Attempts.ShouldBe(job.MaxAttempts);
    job.LastError.ShouldBe("InvalidOperationException: boom");
    job.CompletedAt.ShouldNotBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Run_WithUnknownType_ShouldFailPermanently(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString);
    var id = await SeedAsync(provider, connectionString, Record());

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    var job = await JobAsync(provider, connectionString, id);
    job.State.ShouldBe(JobState.Failed);
    job.LastError!.ShouldContain(JobType);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Run_WhenQueueIsEmpty_ShouldReturnFalse(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString);

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeFalse();
  }

  // End to end: the Ping job is sent through the real mediator; its command is ISystemOnly, so it only
  // succeeds when the scope of the job runs as the system principal.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Run_ShouldExecuteAsSystem(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    bool? isSystem = null;
    await using var services = BuildServices(provider, connectionString);
    long id;

    await using (var scope = services.CreateAsyncScope())
    {
      id = await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync(JobRequest.Create(JobTypes.Ping, "{}"), Ct);
    }

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    var job = await JobAsync(provider, connectionString, id);
    job.State.ShouldBe(JobState.Succeeded);
    job.LastError.ShouldBeNull();
    job.Attempts.ShouldBe(1);
    job.CompletedAt.ShouldNotBeNull();

    // The same check, from inside the scope of the job.
    await using var observed = BuildServices(provider, connectionString, observeUser: user => isSystem = user.IsSystem);
    await SeedAsync(provider, connectionString, Record());

    (await Worker(observed).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    isSystem.ShouldBe(true);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Sweep_ExpiredLeaseWithQueuedTwin_ShouldAbsorb(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString);
    var expired = await SeedAsync(provider, connectionString, Running("finalize:42", DateTimeOffset.UtcNow.AddMinutes(-1)));
    var twin = await SeedAsync(provider, connectionString, Record(dedupKey: "finalize:42"));

    (await Sweeper(services).SweepAsync(Ct)).ShouldBe(1);

    var absorbed = await JobAsync(provider, connectionString, expired);
    absorbed.State.ShouldBe(JobState.Succeeded);
    absorbed.LockedUntil.ShouldBeNull();
    absorbed.LastError!.ShouldContain("gone-worker");
    absorbed.CompletedAt.ShouldNotBeNull();
    (await JobAsync(provider, connectionString, twin)).State.ShouldBe(JobState.Queued);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Sweep_ExpiredLease_ShouldQueueTheJobAgain(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString);
    var expired = await SeedAsync(provider, connectionString, Running("finalize:42", DateTimeOffset.UtcNow.AddMinutes(-1)));
    var alive = await SeedAsync(provider, connectionString, Running(dedupKey: null, DateTimeOffset.UtcNow.AddMinutes(4)));

    (await Sweeper(services).SweepAsync(Ct)).ShouldBe(1);

    var requeued = await JobAsync(provider, connectionString, expired);
    requeued.State.ShouldBe(JobState.Queued);
    requeued.Attempts.ShouldBe(1);
    requeued.LockedBy.ShouldBeNull();
    requeued.LockedUntil.ShouldBeNull();
    (await JobAsync(provider, connectionString, alive)).State.ShouldBe(JobState.Running, "a live lease is never swept");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Sweep_ExpiredLeaseOnLastAttempt_ShouldMarkDeadAndCountIt(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    using var meterFactory = new TestMeterFactory();
    using var recorder = new MeasurementRecorder(meterFactory);
    await using var services = BuildServices(provider, connectionString, meterFactory: meterFactory);
    var record = Running(dedupKey: null, DateTimeOffset.UtcNow.AddMinutes(-1));
    record.Attempts = record.MaxAttempts;
    var id = await SeedAsync(provider, connectionString, record);

    (await Sweeper(services).SweepAsync(Ct)).ShouldBe(1);

    (await JobAsync(provider, connectionString, id)).State.ShouldBe(JobState.Dead);
    recorder.Sum(CodeCoverageMeter.JobsDeadTotal).ShouldBe(1);
  }

  [Fact]
  public async Task Execute_WhenDisabled_ShouldNeverTouchTheQueue()
  {
    var scopeFactory = A.Fake<IServiceScopeFactory>();
    var options = Options.Create(new WorkerOptions { Enabled = false });
    using var worker = new JobWorker(
      scopeFactory,
      new ConfigurationBuilder().Build(),
      options,
      new JobWakeUpSignal(TimeProvider.System),
      new JobHeartbeat(scopeFactory, TimeProvider.System, NullLogger<JobHeartbeat>.Instance),
      A.Fake<IClock>(),
      TimeProvider.System,
      NullLogger<JobWorker>.Instance
    );
    using var sweeper = new LeaseSweeper(scopeFactory, options, TimeProvider.System, NullLogger<LeaseSweeper>.Instance);

    await worker.StartAsync(Ct);
    await sweeper.StartAsync(Ct);
    await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    await sweeper.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), Ct);

    worker.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
    sweeper.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
    A.CallTo(scopeFactory).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(null, 4)]
  [InlineData("not a number", 4)]
  [InlineData("1", 1)]
  [InlineData("8", 8)]
  [InlineData("0", 1)]
  [InlineData("64", 16)]
  public void ReadConcurrency_ShouldFollowTheSettingWithinItsBounds(string? value, int expected)
  {
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?> { ["Processing:WorkerConcurrency"] = value })
                        .Build();

    JobWorker.ReadConcurrency(configuration).ShouldBe(expected);
  }

  private static ServiceProvider BuildServices(
    DatabaseProvider provider,
    string connectionString,
    IJobDispatcher? dispatcher = null,
    Action<ICurrentUser>? observeUser = null,
    IMeterFactory? meterFactory = null)
  {
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                          ["ConnectionStrings:CodeCoverage"] = connectionString,
                          ["CodeCoverage:Database:Provider"] = provider.ToString(),
                        })
                        .Build();
    var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);

    // The meter of the container comes from the meter factory of the test, when a test listens to it.
    if (meterFactory is not null)
    {
      services.AddSingleton(meterFactory);
    }

    services.AddLogging()
            .AddApplication()
            .AddInfrastructure()
            .AddPostgreSqlProvider()
            .AddSqlServerProvider()
            .AddJobProcessing();

    if (dispatcher is not null)
    {
      services.AddScoped(_ => dispatcher);
    }

    if (observeUser is not null)
    {
      services.AddScoped(sp => Observing(sp.GetRequiredService<ICurrentUser>(), observeUser));
    }

    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
  }

  private static IJobDispatcher Observing(ICurrentUser user, Action<ICurrentUser> observe)
  {
    var dispatcher = A.Fake<IJobDispatcher>();
    A.CallTo(() => dispatcher.DispatchAsync(A<JobRecord>._, A<CancellationToken>._))
     .Invokes(() => observe(user))
     .Returns(Task.CompletedTask);

    return dispatcher;
  }

  private static IJobDispatcher Throwing(Exception exception)
  {
    var dispatcher = A.Fake<IJobDispatcher>();
    A.CallTo(() => dispatcher.DispatchAsync(A<JobRecord>._, A<CancellationToken>._)).ThrowsAsync(exception);

    return dispatcher;
  }

  private static JobWorker Worker(IServiceProvider services) =>
    services.GetServices<IHostedService>().OfType<JobWorker>().Single();

  private static LeaseSweeper Sweeper(IServiceProvider services) =>
    services.GetServices<IHostedService>().OfType<LeaseSweeper>().Single();

  private static JobRecord Record(string? dedupKey = null)
  {
    var past = DateTimeOffset.UtcNow.AddMinutes(-1);

    return new JobRecord
    {
      Type = JobType,
      PayloadJson = "{}",
      State = JobState.Queued,
      DedupKey = dedupKey,
      MaxAttempts = 8,
      RunAfter = past,
      FirstQueuedAt = past,
      CorrelationId = "test",
      CreatedAt = past,
    };
  }

  private static JobRecord Running(string? dedupKey, DateTimeOffset lockedUntil)
  {
    var record = Record(dedupKey);
    record.State = JobState.Running;
    record.Attempts = 1;
    record.LockedBy = "gone-worker";
    record.LockedUntil = lockedUntil;

    return record;
  }

  private static async Task<long> SeedAsync(DatabaseProvider provider, string connectionString, JobRecord record)
  {
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    context.Jobs.Add(record);
    await context.SaveChangesAsync(Ct);

    return record.Id;
  }

  private static async Task<JobRecord> JobAsync(DatabaseProvider provider, string connectionString, long id)
  {
    await using var check = DatabaseFixture.CreateContext(provider, connectionString);

    return await check.Jobs.AsNoTracking().SingleAsync(j => j.Id == id, Ct);
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
