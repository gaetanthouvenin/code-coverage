// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseSettingsConfigurationProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Settings;

[Collection(DatabaseCollection.Name)]
public sealed class DatabaseSettingsConfigurationProviderTests(DatabaseFixture database)
{
  #region Fields

  // Refuses every connection at once: the failure path without Docker.
  private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=codecoverage;Timeout=1";

  private const string UnreachableSqlServer = "Server=127.0.0.1,1;Database=codecoverage;Connect Timeout=1;Encrypt=False";

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The database exists, the Api has not migrated it yet: no setting, no error.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Load_WithoutTable_ShouldReturnEmpty(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.GetService<IRelationalDatabaseCreator>().CreateAsync(Ct);
    }

    var store = new DatabaseSettingsConfigurationProvider(connectionString, provider);

    store.Load();

    store.GetChildKeys([], null).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Load_WithoutDatabase_ShouldReturnEmpty(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    var store = new DatabaseSettingsConfigurationProvider(connectionString, provider);

    store.Load();

    store.GetChildKeys([], null).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Load_ForEachProvider_ShouldExposeValues(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.Database.MigrateAsync(Ct);
      context.Settings.AddRange(
        Stored("GitHub:AppId", "123456"),
        Stored("Processing:WorkerConcurrency", "8"),
        Stored("GitHub:PrivateKeyPem", "CfDJ8-protected-form", isSecret: true),
        Stored("Badges:Label", null)
      );
      await context.SaveChangesAsync(Ct);
    }

    var configuration = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();

    configuration["GitHub:AppId"].ShouldBe("123456");
    configuration["processing:workerconcurrency"].ShouldBe("8");
    configuration["GitHub:PrivateKeyPem"].ShouldBe("CfDJ8-protected-form");
    configuration.GetSection("Badges").Exists().ShouldBeFalse();
  }

  // The store is the weakest source: any other source wins over it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Load_WhenStrongerSourceProvidesKey_ShouldBeOverridden(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.Database.MigrateAsync(Ct);
      context.Settings.AddRange(Stored("GitHub:AppId", "from-store"), Stored("GitHub:ClientId", "from-store"));
      await context.SaveChangesAsync(Ct);
    }

    var builder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["GitHub:AppId"] = "from-appsettings",
    });

    // Added last, still inserted first.
    var configuration = builder.AddCodeCoverageSettingsStore(connectionString, provider).Build();

    configuration["GitHub:AppId"].ShouldBe("from-appsettings");
    configuration["GitHub:ClientId"].ShouldBe("from-store");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Refresh_AfterRowChanged_ShouldExposeNewValueAndNotify(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    var configuration = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    var notified = false;
    configuration.GetReloadToken().RegisterChangeCallback(_ => notified = true, null);

    context.Settings.Add(Stored("Badges:Label", "cov"));
    await context.SaveChangesAsync(Ct);
    DatabaseSettingsConfigurationProvider.RefreshAll(configuration);

    configuration["Badges:Label"].ShouldBe("cov");
    notified.ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Load_ShouldRecordTheRevisionOfTheStore(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    var store = new DatabaseSettingsConfigurationProvider(connectionString, provider);

    store.Load();
    store.Revision.ShouldBeNull();
    (await store.ReadRevisionAsync(Ct)).ShouldBeNull();

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);
    await context.SettingsRevisions.ExecuteUpdateAsync(s => s.SetProperty(r => r.Revision, 41L), Ct);

    store.Load();

    store.Revision.ShouldBe(41);
    (await store.ReadRevisionAsync(Ct)).ShouldBe(41);
  }

  [Fact]
  public async Task ReadRevision_WhenServerUnreachable_ShouldThrowDbException()
  {
    var store = new DatabaseSettingsConfigurationProvider(UnreachableDatabase, DatabaseProvider.PostgreSql);

    await Should.ThrowAsync<System.Data.Common.DbException>(() => store.ReadRevisionAsync(Ct));
  }

  [Fact]
  public void Load_WhenServerUnreachable_ShouldReturnEmpty()
  {
    var store = new DatabaseSettingsConfigurationProvider(UnreachableDatabase, DatabaseProvider.PostgreSql);

    Should.NotThrow(store.Load);

    store.GetChildKeys([], null).ShouldBeEmpty();
  }

  // A refresh that fails must never bring the defaults back.
  [Fact]
  public void Refresh_WhenServerUnreachable_ShouldKeepPreviousValues()
  {
    var store = new DatabaseSettingsConfigurationProvider(UnreachableDatabase, DatabaseProvider.PostgreSql);
    store.Set("GitHub:AppId", "123456");

    store.Refresh();

    store.TryGet("GitHub:AppId", out var value).ShouldBeTrue();
    value.ShouldBe("123456");
  }

  [Fact]
  public void RefreshAll_WithNullRoot_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => DatabaseSettingsConfigurationProvider.RefreshAll(null!))
          .ParamName.ShouldBe("root");
  }

  [Fact]
  public void AddCodeCoverageSettingsStore_ShouldInsertTheWeakestSource()
  {
    var builder = new ConfigurationBuilder().AddInMemoryCollection();

    builder.AddCodeCoverageSettingsStore(UnreachableDatabase, DatabaseProvider.SqlServer);

    var source = builder.Sources[0].ShouldBeOfType<DatabaseSettingsConfigurationSource>();
    source.ConnectionString.ShouldBe(UnreachableDatabase);
    source.Provider.ShouldBe(DatabaseProvider.SqlServer);
    builder.Sources.Count.ShouldBe(2);
  }

  [Fact]
  public void AddCodeCoverageSettingsStore_WithInvalidArguments_ShouldThrow()
  {
    var builder = new ConfigurationBuilder();

    Should.Throw<ArgumentNullException>(
      () => ((IConfigurationBuilder)null!).AddCodeCoverageSettingsStore(UnreachableDatabase, DatabaseProvider.PostgreSql)
    );
    Should.Throw<ArgumentException>(() => builder.AddCodeCoverageSettingsStore(" ", DatabaseProvider.PostgreSql));
    Should.Throw<ArgumentOutOfRangeException>(() => builder.AddCodeCoverageSettingsStore(UnreachableDatabase, (DatabaseProvider)42));
  }

  [Fact]
  public void AddCodeCoverageSettingsStore_FromBootstrapSettings_ShouldUseConnectionStringAndProvider()
  {
    using var configuration = new ConfigurationManager();
    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:CodeCoverage"] = UnreachableSqlServer,
      ["CodeCoverage:Database:Provider"] = "SqlServer",
    });

    configuration.AddCodeCoverageSettingsStore();

    var source = ((IConfigurationBuilder)configuration).Sources[0].ShouldBeOfType<DatabaseSettingsConfigurationSource>();
    source.ConnectionString.ShouldBe(UnreachableSqlServer);
    source.Provider.ShouldBe(DatabaseProvider.SqlServer);
  }

  [Fact]
  public void AddCodeCoverageSettingsStore_WithoutConnectionString_ShouldAddNothing()
  {
    using var configuration = new ConfigurationManager();

    configuration.AddCodeCoverageSettingsStore();

    ((IConfigurationBuilder)configuration).Sources.OfType<DatabaseSettingsConfigurationSource>().ShouldBeEmpty();
  }

  private static StoredSetting Stored(string key, string? value, bool isSecret = false) =>
    new() { Key = key, Value = value, IsSecret = isSecret, UpdatedAt = Now, UpdatedBy = "octocat" };

  #endregion
}
