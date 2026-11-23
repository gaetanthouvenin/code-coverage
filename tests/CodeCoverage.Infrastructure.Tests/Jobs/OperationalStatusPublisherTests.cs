// ------------------------------------------------------------------------------------------------
// <copyright file="OperationalStatusPublisherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

// The heartbeat and the quotas written by the worker of the Api for the other processes (M3.13a), on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class OperationalStatusPublisherTests(DatabaseFixture database)
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Publish_ShouldUpsertHeartbeatAndChangedQuotas(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var setup = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await setup.Database.MigrateAsync(Ct);
      setup.WorkerHeartbeats.Add(WorkerHeartbeat.Create("stopped:1", Now.AddDays(-3), Now.AddHours(-25)));
      await setup.SaveChangesAsync(Ct);
    }

    var now = Now;
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).ReturnsLazily(() => now);
    var pulse = new WorkerPulse();
    using var meters = new TestMeterFactory();
    var quotas = new ForgeQuotaTracker(new CodeCoverageMeter(meters));
    await using var services = new ServiceCollection().AddScoped(_ => DatabaseFixture.CreateContext(provider, connectionString)).BuildServiceProvider();
    var publisher = new OperationalStatusPublisher(
      services.GetRequiredService<IServiceScopeFactory>(),
      pulse,
      quotas,
      clock,
      TimeProvider.System,
      NullLogger<OperationalStatusPublisher>.Instance);

    pulse.Beat(Now);
    quotas.Report(Forge.GitHub, "42", 4321, 5000, Now.AddMinutes(30), Now);
    quotas.Report(Forge.GitHub, ForgeQuota.AppConnection, 4999, 5000, null, Now);
    await publisher.PublishAsync(Ct);

    now = Now.AddSeconds(15);
    pulse.Beat(now);
    quotas.Report(Forge.GitHub, "42", 4300, 5000, Now.AddMinutes(30), now);
    await publisher.PublishAsync(Ct);

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    var heartbeat = (await check.WorkerHeartbeats.ToListAsync(Ct)).ShouldHaveSingleItem();
    heartbeat.Host.ShouldBe(OperationalStatusPublisher.Host);
    heartbeat.LastBeatAt.ShouldBe(Now.AddSeconds(15));
    var rows = await check.ForgeQuotas.OrderBy(static q => q.Connection).ToListAsync(Ct);
    rows.Select(static q => (q.Forge, q.Connection, q.Remaining, q.ObservedAt)).ShouldBe([(Forge.GitHub, "42", 4300L, Now.AddSeconds(15)), (Forge.GitHub, ForgeQuota.AppConnection, 4999L, Now)]);
    rows[0].Limit.ShouldBe(5000);
  }

  // Two Api replicas report the same installation: the row keeps the newest observation, whichever process writes last.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Publish_OlderObservation_ShouldNotOverwriteANewerRow(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var setup = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await setup.Database.MigrateAsync(Ct);
      setup.ForgeQuotas.Add(ForgeQuota.Create(Forge.GitHub, "42", 4200, 5000, Now.AddMinutes(30), Now.AddSeconds(10)));
      await setup.SaveChangesAsync(Ct);
    }

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now.AddSeconds(15));
    using var meters = new TestMeterFactory();
    var quotas = new ForgeQuotaTracker(new CodeCoverageMeter(meters));
    await using var services = new ServiceCollection().AddScoped(_ => DatabaseFixture.CreateContext(provider, connectionString)).BuildServiceProvider();
    var publisher = new OperationalStatusPublisher(
      services.GetRequiredService<IServiceScopeFactory>(),
      new WorkerPulse(),
      quotas,
      clock,
      TimeProvider.System,
      NullLogger<OperationalStatusPublisher>.Instance);

    quotas.Report(Forge.GitHub, "42", 4321, 5000, Now.AddMinutes(30), Now);
    await publisher.PublishAsync(Ct);

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    var row = (await check.ForgeQuotas.ToListAsync(Ct)).ShouldHaveSingleItem();
    (row.Remaining, row.ObservedAt).ShouldBe((4200L, Now.AddSeconds(10)));
  }

  // M7.28: the same connection key on two forges is two quotas, each kept in its own row.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task PublishAsync_TwoForges_ShouldWriteOneQuotaEach(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var setup = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await setup.Database.MigrateAsync(Ct);
    }

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);
    using var meters = new TestMeterFactory();
    var quotas = new ForgeQuotaTracker(new CodeCoverageMeter(meters));
    await using var services = new ServiceCollection().AddScoped(_ => DatabaseFixture.CreateContext(provider, connectionString)).BuildServiceProvider();
    var publisher = new OperationalStatusPublisher(
      services.GetRequiredService<IServiceScopeFactory>(),
      new WorkerPulse(),
      quotas,
      clock,
      TimeProvider.System,
      NullLogger<OperationalStatusPublisher>.Instance);

    quotas.Report(Forge.GitHub, "42", 4321, 5000, Now.AddMinutes(30), Now);
    quotas.Report(Forge.Bitbucket, "42", 900, 1000, Now.AddHours(1), Now);
    await publisher.PublishAsync(Ct);

    quotas.Report(Forge.Bitbucket, "42", 850, 1000, Now.AddHours(1), Now.AddSeconds(15));
    await publisher.PublishAsync(Ct);

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    var rows = await check.ForgeQuotas.ToListAsync(Ct);
    rows.Select(static q => (q.Forge, q.Connection, q.Remaining))
        .OrderBy(static q => q.Forge)
        .ShouldBe([(Forge.GitHub, "42", 4321L), (Forge.Bitbucket, "42", 850L)]);
  }

  // Without a beat of the worker, nothing is written: a process that never ran its worker has no heartbeat.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Publish_WithoutBeat_ShouldWriteNoHeartbeat(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var setup = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await setup.Database.MigrateAsync(Ct);
    }

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);
    using var meters = new TestMeterFactory();
    await using var services = new ServiceCollection().AddScoped(_ => DatabaseFixture.CreateContext(provider, connectionString)).BuildServiceProvider();
    var publisher = new OperationalStatusPublisher(
      services.GetRequiredService<IServiceScopeFactory>(),
      new WorkerPulse(),
      new ForgeQuotaTracker(new CodeCoverageMeter(meters)),
      clock,
      TimeProvider.System,
      NullLogger<OperationalStatusPublisher>.Instance);

    await publisher.PublishAsync(Ct);

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    (await check.WorkerHeartbeats.CountAsync(Ct)).ShouldBe(0);
  }

  #endregion
}
