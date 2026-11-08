// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseMigratorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class DatabaseMigratorTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task AddDatabaseMigration_ShouldRegisterTheMigratorAsHostedService()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetServices<IHostedService>().OfType<DatabaseMigrator>().ShouldHaveSingleItem();
  }

  // Without a connection string, opening a context would throw: the migrator must not try.
  [Fact]
  public async Task StartAsync_WhenMigrateOnStartupIsOff_ShouldNotTouchTheDatabase()
  {
    await using var services = Build(new Dictionary<string, string?>
    {
      ["CodeCoverage:Database:MigrateOnStartup"] = "false",
    });

    await Should.NotThrowAsync(() => Migrator(services).StartAsync(Ct));
  }

  [Fact]
  public async Task StartAsync_WhenMigrateOnStartupIsOnWithoutConnectionString_ShouldThrow()
  {
    await using var services = Build(new Dictionary<string, string?>());

    var exception = await Should.ThrowAsync<InvalidOperationException>(
      () => Migrator(services).StartAsync(Ct)
    );

    exception.Message.ShouldContain("ConnectionStrings:CodeCoverage");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task StartAsync_ForEachProvider_ShouldApplyEveryMigration(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    await using var services = Build(new Dictionary<string, string?>
    {
      ["ConnectionStrings:CodeCoverage"] = connectionString,
      ["CodeCoverage:Database:Provider"] = provider.ToString(),
    });
    var migrator = Migrator(services);

    await migrator.StartAsync(Ct);

    // A second start finds the schema up to date and changes nothing.
    await migrator.StartAsync(Ct);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    (await context.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
    (await context.Database.GetAppliedMigrationsAsync(Ct)).ShouldBe(context.Database.GetMigrations());
  }

  // AddHealthChecks registers its publisher as a hosted service too.
  private static DatabaseMigrator Migrator(ServiceProvider services) =>
    services.GetServices<IHostedService>().OfType<DatabaseMigrator>().Single();

  private static ServiceProvider Build(Dictionary<string, string?> settings)
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    return new ServiceCollection()
           .AddSingleton<IConfiguration>(configuration)
           .AddSingleton(A.Fake<ISecretProtector>())
           .AddLogging()
           .AddInfrastructure()
           .AddDatabaseMigration()
           .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
  }

  #endregion
}
