// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingsProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Settings;

// In these tests, one context stands for one DI scope.
[Collection(DatabaseCollection.Name)]
public sealed class OrganizationSettingsProviderTests(DatabaseFixture database)
{
  #region Fields

  private const string Yaml = "coverage:\n  range: 60..90\n";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_NoRow_ShouldUseCatalogDefault(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    var snapshot = await ReadAsync(organizations, organizations.FirstId);

    snapshot.ShouldBe(new OrganizationSettingsSnapshot("coverage", true, true, null));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_OrganizationRows_ShouldOverrideCatalogDefault(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await organizations.AddSettingAsync(organizations.FirstId, SettingDefinitions.NotificationsStatusContextPrefix, "codecov", Ct);
    await organizations.AddSettingAsync(organizations.FirstId, SettingDefinitions.NotificationsStatusesEnabled, "false", Ct);
    await organizations.AddSettingAsync(organizations.FirstId, SettingDefinitions.NotificationsCommentsEnabled, "false", Ct);
    await organizations.AddSettingAsync(organizations.FirstId, SettingDefinitions.CoverageOrganizationYaml, Yaml, Ct);

    var snapshot = await ReadAsync(organizations, organizations.FirstId);

    snapshot.ShouldBe(new OrganizationSettingsSnapshot("codecov", false, false, Yaml));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_OrganizationRow_ShouldKeepCatalogDefaultOfTheOtherSettings(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await organizations.AddSettingAsync(organizations.FirstId, SettingDefinitions.NotificationsCommentsEnabled, "false", Ct);

    var snapshot = await ReadAsync(organizations, organizations.FirstId);

    snapshot.ShouldBe(new OrganizationSettingsSnapshot("coverage", true, false, null));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_OtherOrganizationRow_ShouldBeIgnored(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await organizations.AddSettingAsync(organizations.SecondId, SettingDefinitions.NotificationsStatusContextPrefix, "second", Ct);

    (await ReadAsync(organizations, organizations.FirstId)).StatusContextPrefix.ShouldBe("coverage");
    (await ReadAsync(organizations, organizations.SecondId)).StatusContextPrefix.ShouldBe("second");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_Twice_ShouldReadOnce(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var settings = new OrganizationSettingsProvider(context);

    var first = await settings.GetAsync(Ct);
    await organizations.AddSettingAsync(organizations.FirstId, SettingDefinitions.NotificationsStatusContextPrefix, "changed", Ct);
    var second = await settings.GetAsync(Ct);

    second.ShouldBeSameAs(first);
    second.StatusContextPrefix.ShouldBe("coverage");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_WithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(() => new OrganizationSettingsProvider(context).GetAsync(Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Get_RowChanged_ShouldBeVisibleInTheNextScopeWithoutIncrementingTheRevision(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var revision = await RevisionAsync(organizations);

    (await ReadAsync(organizations, organizations.FirstId)).StatusesEnabled.ShouldBeTrue();

    await organizations.AddSettingAsync(organizations.FirstId, SettingDefinitions.NotificationsStatusesEnabled, "false", Ct);

    (await ReadAsync(organizations, organizations.FirstId)).StatusesEnabled.ShouldBeFalse();
    (await RevisionAsync(organizations)).ShouldBe(revision);
  }

  // A singleton would share the values of one organization with the others.
  [Fact]
  public void AddInfrastructure_ShouldRegisterTheProviderPerScope()
  {
    var services = new ServiceCollection().AddInfrastructure();

    services.Single(static d => d.ServiceType == typeof(IOrganizationSettingsProvider)).Lifetime.ShouldBe(ServiceLifetime.Scoped);
  }

  private static async Task<OrganizationSettingsSnapshot> ReadAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await new OrganizationSettingsProvider(context).GetAsync(Ct);
  }

  private static async Task<long> RevisionAsync(TwoOrganizationDatabase organizations)
  {
    await using var context = organizations.ContextWithoutOrganization();

    return await context.SettingsRevisions.AsNoTracking().Select(static r => r.Revision).SingleAsync(Ct);
  }

  #endregion
}
