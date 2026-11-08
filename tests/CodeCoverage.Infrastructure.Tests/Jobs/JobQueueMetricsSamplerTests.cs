// ------------------------------------------------------------------------------------------------
// <copyright file="JobQueueMetricsSamplerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.Metrics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

// The sampler of the job queue metrics. The test that measures a real queue runs on both engines and
// is skipped, with the reason, when Docker is not available.
[Collection(DatabaseCollection.Name)]
public sealed class JobQueueMetricsSamplerTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  private const int SampleFailedEventId = 1240;

  private readonly TestMeterFactory _meterFactory = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Sample_ShouldReportTheMeasureToTheJobGauges(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    await using var services = BuildServices(provider, connectionString);
    using var recorder = new MeasurementRecorder(_meterFactory);
    var now = DateTimeOffset.UtcNow;
    await SeedAsync(provider, connectionString, Record("FinalizeCommit", now.AddMinutes(-2)), Record("NotifyCommit", now.AddMinutes(10)));
    var sampler = new JobQueueMetricsSampler(
      services.GetRequiredService<IServiceScopeFactory>(),
      services.GetRequiredService<CodeCoverageMeter>(),
      TimeProvider.System,
      NullLogger<JobQueueMetricsSampler>.Instance
    );

    var snapshot = await sampler.SampleAsync(Ct);

    snapshot.ShouldNotBeNull();
    recorder.Collect();
    var queued = recorder.Of(CodeCoverageMeter.JobsQueued);
    queued.Count.ShouldBe(2);
    queued.Single(m => Equals(m.Tags["type"], "FinalizeCommit")).Value.ShouldBe(1);
    queued.Single(m => Equals(m.Tags["type"], "NotifyCommit")).Value.ShouldBe(1);
    recorder.Of(CodeCoverageMeter.JobOldestQueuedAgeSeconds).ShouldHaveSingleItem().Value.ShouldBeInRange(115, 180);
  }

  // Without an OTLP endpoint nobody collects the gauges: the database is never queried.
  [Fact]
  public async Task Sample_WhenNobodyListens_ShouldNotMeasure()
  {
    var scopeFactory = A.Fake<IServiceScopeFactory>();
    var sampler = new JobQueueMetricsSampler(
      scopeFactory,
      new CodeCoverageMeter(_meterFactory),
      TimeProvider.System,
      NullLogger<JobQueueMetricsSampler>.Instance
    );

    (await sampler.SampleAsync(Ct)).ShouldBeNull();

    A.CallTo(scopeFactory).MustNotHaveHappened();
  }

  // The service runs in the background from StartAsync: the clock is moved on until its timer ticks.
  [Fact]
  public async Task Execute_WhenTheMeasureFails_ShouldLogAndMeasureAgainAtTheNextTick()
  {
    var scopeFactory = A.Fake<IServiceScopeFactory>();
    A.CallTo(() => scopeFactory.CreateScope()).Throws(new InvalidOperationException("The database is unreachable."));
    var time = new FakeTimeProvider();
    var logger = new CapturingLogger<JobQueueMetricsSampler>();
    using var recorder = new MeasurementRecorder(_meterFactory);
    using var sampler = new JobQueueMetricsSampler(scopeFactory, new CodeCoverageMeter(_meterFactory), time, logger);

    await sampler.StartAsync(Ct);
    await AdvanceUntilAsync(time, () => !logger.Entries.IsEmpty);
    var first = logger.Entries.Count;
    await AdvanceUntilAsync(time, () => logger.Entries.Count > first);
    await sampler.StopAsync(Ct);

    sampler.ExecuteTask!.IsCompletedSuccessfully.ShouldBeTrue("a failed measure never stops the host");
    logger.Entries.Count.ShouldBeGreaterThanOrEqualTo(2);
    logger.Entries.ShouldAllBe(e => e.EventId.Id == SampleFailedEventId && e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    A.CallTo(() => scopeFactory.CreateScope()).MustHaveHappenedANumberOfTimesMatching(n => n == logger.Entries.Count);
  }

  [Fact]
  public void Ctor_WithNullArguments_ShouldThrowArgumentNullException()
  {
    var scopeFactory = A.Fake<IServiceScopeFactory>();
    var meter = new CodeCoverageMeter(_meterFactory);
    var logger = NullLogger<JobQueueMetricsSampler>.Instance;

    Should.Throw<ArgumentNullException>(() => new JobQueueMetricsSampler(null!, meter, TimeProvider.System, logger));
    Should.Throw<ArgumentNullException>(() => new JobQueueMetricsSampler(scopeFactory, null!, TimeProvider.System, logger));
    Should.Throw<ArgumentNullException>(() => new JobQueueMetricsSampler(scopeFactory, meter, null!, logger));
    Should.Throw<ArgumentNullException>(() => new JobQueueMetricsSampler(scopeFactory, meter, TimeProvider.System, null!));
  }

  // Moves the clock on by one interval, then waits a little for the tick, until the condition holds.
  private static async Task AdvanceUntilAsync(FakeTimeProvider time, Func<bool> condition)
  {
    var deadline = DateTime.UtcNow.AddSeconds(10);

    while (!condition())
    {
      DateTime.UtcNow.ShouldBeLessThan(deadline, "the sampler did not reach the expected state in time");
      time.Advance(JobQueueMetricsSampler.Interval);

      for (var i = 0; i < 20 && !condition(); i++)
      {
        await Task.Delay(10, Ct);
      }
    }
  }

  private static JobRecord Record(string type, DateTimeOffset runAfter) =>
    new()
    {
      Type = type,
      PayloadJson = "{}",
      State = JobState.Queued,
      MaxAttempts = 8,
      RunAfter = runAfter,
      FirstQueuedAt = runAfter,
      CorrelationId = "test",
      CreatedAt = runAfter,
    };

  private static async Task SeedAsync(DatabaseProvider provider, string connectionString, params JobRecord[] records)
  {
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    context.Jobs.AddRange(records);
    await context.SaveChangesAsync(Ct);
  }

  // The meter of the container comes from the meter factory of the test, which the recorder listens to.
  private ServiceProvider BuildServices(DatabaseProvider provider, string connectionString)
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
           .AddSingleton<IMeterFactory>(_meterFactory)
           .AddLogging()
           .AddInfrastructure()
           .AddPostgreSqlProvider()
           .AddSqlServerProvider()
           .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
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
