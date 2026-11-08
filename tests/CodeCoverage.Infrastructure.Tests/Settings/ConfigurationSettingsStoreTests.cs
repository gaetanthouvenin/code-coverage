// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationSettingsStoreTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Settings;

[Collection(DatabaseCollection.Name)]
public sealed class ConfigurationSettingsStoreTests(DatabaseFixture database)
{
  #region Fields

  // Refuses every connection at once: the configuration tests never reach a database.
  private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=codecoverage;Timeout=1";

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  private static readonly SettingDefinition AppId = SettingDefinitions.Find(SettingDefinitions.GitHubAppId)!;

  private static readonly SettingDefinition Organization = SettingDefinitions.Find(SettingDefinitions.GitHubOrganization)!;

  private static readonly SettingDefinition WorkerConcurrency =
    SettingDefinitions.Find(SettingDefinitions.ProcessingWorkerConcurrency)!;

  private static readonly SettingDefinition PrivateKey = SettingDefinitions.Find(SettingDefinitions.GitHubPrivateKeyPem)!;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A real environment variable, under a prefix of its own so that no other test sees it.
  [Fact]
  public async Task GetSource_WhenEnvironmentProvidesKey_ShouldReturnEnvironment()
  {
    var prefix = $"CCTEST_{Guid.NewGuid():N}_";
    Environment.SetEnvironmentVariable(prefix + "GitHub__AppId", "654321");

    try
    {
      var configuration = new ConfigurationBuilder()
                          .AddCodeCoverageSettingsStore(UnreachableDatabase, DatabaseProvider.PostgreSql)
                          .AddEnvironmentVariables(prefix)
                          .Build();
      StoreProvider(configuration).Set(SettingDefinitions.GitHubAppId, "123456");
      await using var context = UnusedContext();

      var store = CreateStore(context, configuration);

      store.GetSource(AppId).ShouldBe(SettingSource.Environment);
      configuration[SettingDefinitions.GitHubAppId].ShouldBe("654321");
    }
    finally
    {
      Environment.SetEnvironmentVariable(prefix + "GitHub__AppId", null);
    }
  }

  [Fact]
  public async Task GetSource_WhenOnlyTheStoreProvidesKey_ShouldReturnStore()
  {
    var configuration = new ConfigurationBuilder()
                        .AddCodeCoverageSettingsStore(UnreachableDatabase, DatabaseProvider.PostgreSql)
                        .AddInMemoryCollection(new Dictionary<string, string?> { ["GitHub:ClientId"] = "other" })
                        .Build();
    StoreProvider(configuration).Set(SettingDefinitions.GitHubAppId, "123456");
    await using var context = UnusedContext();

    var store = CreateStore(context, configuration);

    store.GetSource(AppId).ShouldBe(SettingSource.Store);
    store.GetSource(Organization).ShouldBe(SettingSource.Default);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_ShouldIncrementRevisionInSameTransaction(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var configuration = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      var store = CreateStore(context, configuration);

      await store.SaveAsync(WorkerConcurrency, "8", Ct);
      await store.SaveAsync(WorkerConcurrency, "6", Ct);

      context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    // The host that saves sees the change at once.
    configuration[SettingDefinitions.ProcessingWorkerConcurrency].ShouldBe("6");

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    (await check.SettingsRevisions.SingleAsync(Ct)).Revision.ShouldBe(2);
    var saved = await check.Settings.SingleAsync(Ct);
    saved.Key.ShouldBe(SettingDefinitions.ProcessingWorkerConcurrency);
    saved.Value.ShouldBe("6");
    saved.IsSecret.ShouldBeFalse();
    saved.UpdatedAt.ShouldBe(Now);
    saved.UpdatedBy.ShouldBe("octocat");
  }

  // Without its revision, a value would be invisible to the other hosts: it is not written at all.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_WhenRevisionCannotBeIncremented_ShouldWriteNothing(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var configuration = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.SettingsRevisions.ExecuteDeleteAsync(Ct);

      await Should.ThrowAsync<InvalidOperationException>(
        () => CreateStore(context, configuration).SaveAsync(WorkerConcurrency, "8", Ct)
      );

      context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    (await check.Settings.AnyAsync(Ct)).ShouldBeFalse();
    configuration[SettingDefinitions.ProcessingWorkerConcurrency].ShouldBeNull();
  }

  // A secret only reaches the configuration in its protected form.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_Secret_ShouldExposeOnlyTheProtectedForm(DatabaseProvider provider)
  {
    const string Pem = "-----BEGIN RSA PRIVATE KEY----- s3cr3t";
    var connectionString = await MigratedDatabaseAsync(provider);
    var configuration = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
    var protectedPem = protector.Protect(Pem);

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await CreateStore(context, configuration).SaveAsync(PrivateKey, protectedPem, Ct);
    }

    configuration[SettingDefinitions.GitHubPrivateKeyPem].ShouldBe(protectedPem);
    configuration.AsEnumerable().Select(static p => p.Value).ShouldNotContain(Pem);
    protector.Unprotect(configuration[SettingDefinitions.GitHubPrivateKeyPem]!).ShouldBe(Pem);

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    (await check.Settings.SingleAsync(Ct)).IsSecret.ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_ForEachSource_ShouldReturnValueSourceAndAuthor(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var configuration = new ConfigurationBuilder()
                        .AddCodeCoverageSettingsStore(connectionString, provider)
                        .AddInMemoryCollection(new Dictionary<string, string?> { [SettingDefinitions.GitHubAppId] = "654321" })
                        .Build();

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var store = CreateStore(context, configuration);
    await store.SaveAsync(AppId, "123456", Ct);
    await store.SaveAsync(WorkerConcurrency, "8", Ct);

    var overridden = await store.GetAsync(AppId, Ct);
    overridden.Source.ShouldBe(SettingSource.Environment);
    overridden.Value.ShouldBe("654321");
    overridden.IsEditable.ShouldBeFalse();
    overridden.UpdatedBy.ShouldBe("octocat");

    var stored = await store.GetAsync(WorkerConcurrency, Ct);
    stored.Source.ShouldBe(SettingSource.Store);
    stored.Value.ShouldBe("8");
    stored.UpdatedAt.ShouldBe(Now);

    var byDefault = await store.GetAsync(Organization, Ct);
    byDefault.Source.ShouldBe(SettingSource.Default);
    byDefault.Value.ShouldBe(Organization.Default);
    byDefault.UpdatedAt.ShouldBeNull();

    var all = await store.ListAsync(Ct);
    all.Select(static v => v.Key).ShouldBe(SettingDefinitions.All.Select(static d => d.Key));
    all.Single(static v => v.Key == SettingDefinitions.ProcessingWorkerConcurrency).Value.ShouldBe("8");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Reset_ShouldRemoveValueAndIncrementRevision(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var configuration = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      var store = CreateStore(context, configuration);
      await store.SaveAsync(WorkerConcurrency, "8", Ct);

      await store.ResetAsync(WorkerConcurrency, Ct);

      // Nothing left to reset: the revision does not move.
      await store.ResetAsync(WorkerConcurrency, Ct);
    }

    configuration[SettingDefinitions.ProcessingWorkerConcurrency].ShouldBeNull();

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    (await check.Settings.AnyAsync(Ct)).ShouldBeFalse();
    (await check.SettingsRevisions.SingleAsync(Ct)).Revision.ShouldBe(2);
  }

  [Fact]
  public async Task Store_WithNullArguments_ShouldThrowArgumentNullException()
  {
    await using var context = UnusedContext();
    var store = CreateStore(context, new ConfigurationBuilder().Build());

    await Should.ThrowAsync<ArgumentNullException>(() => store.GetAsync(null!, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => store.SaveAsync(null!, "8", Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => store.SaveAsync(WorkerConcurrency, null!, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => store.ResetAsync(null!, Ct));
    Should.Throw<ArgumentNullException>(() => store.GetSource(null!));
  }

  private static ConfigurationSettingsStore CreateStore(CodeCoverageDbContext context, IConfiguration configuration)
  {
    var user = new CurrentUserContext();
    user.SetUser(7, "octocat", isAdmin: true);
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    return new ConfigurationSettingsStore(context, configuration, user, clock);
  }

  private static DatabaseSettingsConfigurationProvider StoreProvider(IConfigurationRoot configuration) =>
    configuration.Providers.OfType<DatabaseSettingsConfigurationProvider>().Single();

  // Never opened: these tests only read the configuration.
  private static CodeCoverageDbContext UnusedContext() =>
    DatabaseFixture.CreateContext(DatabaseProvider.PostgreSql, UnreachableDatabase);

  private async Task<string> MigratedDatabaseAsync(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    return connectionString;
  }

  #endregion
}
