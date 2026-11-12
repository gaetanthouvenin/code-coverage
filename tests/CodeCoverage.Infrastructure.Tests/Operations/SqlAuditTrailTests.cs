// ------------------------------------------------------------------------------------------------
// <copyright file="SqlAuditTrailTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Audit;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Operations;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Operations;

// The audit log of the instance (M3.41) on both engines: the rows of an organization are never read, nor the events of a
// token bound to one, and the families and the search answer the same everywhere.
[Collection(DatabaseCollection.Name)]
public sealed class SqlAuditTrailTests(DatabaseFixture database)
{
  #region Fields

  private const string InstanceTokenTarget = "instance-token";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListInstance_ShouldReadTheInstanceRowsOnlyAndNoEventOfAnOrganizationToken(DatabaseProvider provider)
  {
    var connectionString = await SeedAsync(provider);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var page = await new SqlAuditTrail(context).ListInstanceAsync(null, null, 1, 50, Ct);

    page.Items.Select(static e => e.Action).ShouldBe(
    [
      "Repositories.Activate", "Jobs.Retry", "ApiTokens.Create", "Organization.Admit", "Settings.Update", "Settings.Reset", "Settings.Reset", "Settings.ReplaceSecret",
    ]);
    page.TotalCount.ShouldBe(8);
    page.Items.ShouldNotContain(static e => e.Target == "org-entry");
    page.Items.Single(static e => e.Action == "ApiTokens.Create").Target.ShouldBe(InstanceTokenTarget);
    page.Items.Single(static e => e.Action == "Repositories.Activate").Category.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListInstance_ByCategory_ShouldKeepItsFamilyOnly(DatabaseProvider provider)
  {
    var connectionString = await SeedAsync(provider);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var trail = new SqlAuditTrail(context);

    (await Targets(trail, AuditCategory.Secrets)).ShouldBe([SettingDefinitions.GitHubWebhookSecret, SettingDefinitions.GitHubClientSecret]);
    (await Targets(trail, AuditCategory.Settings)).ShouldBe(["Uploads:MaxFilesPerUpload", SettingDefinitions.UploadsMaxFilesPerUpload]);
    (await Targets(trail, AuditCategory.Organizations)).ShouldBe(["GitHub/Octo-Org"]);
    (await Targets(trail, AuditCategory.Tokens)).ShouldBe([InstanceTokenTarget]);
    (await Targets(trail, AuditCategory.Retries)).ShouldBe(["42"]);
  }

  // Lower case on both sides: the same rows on PostgreSQL (case-sensitive) and SQL Server (case-insensitive collation).
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListInstance_Search_ShouldIgnoreTheCaseOnBothEngines(DatabaseProvider provider)
  {
    var connectionString = await SeedAsync(provider);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var trail = new SqlAuditTrail(context);

    (await trail.ListInstanceAsync(null, "OCTO", 1, 50, Ct)).Items.Select(static e => e.Target).ShouldBe(["GitHub/Octo-Org"]);
    (await trail.ListInstanceAsync(null, "  maxfiles ", 1, 50, Ct)).Items.Count.ShouldBe(2);

    var second = await trail.ListInstanceAsync(null, null, 2, 3, Ct);
    second.Items.Select(static e => e.Action).ShouldBe(["Organization.Admit", "Settings.Update", "Settings.Reset"]);
    second.TotalCount.ShouldBe(8);
  }

  private static async Task<IReadOnlyList<string>> Targets(IAuditReader trail, AuditCategory category) =>
    [.. (await trail.ListInstanceAsync(category, null, 1, 50, Ct)).Items.Select(static e => e.Target)];

  // One row a minute, the oldest first; the token of the organization had its events recorded outside of it (old rows).
  private async Task<string> SeedAsync(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    var user = new CurrentUserContext();
    user.SetSystem();
    var minute = 0;

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    var owner = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    context.Users.Add(owner);
    var organization = Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now);
    organization.Admit("octocat", Now);
    context.Organizations.Add(organization);
    await context.SaveChangesAsync(Ct);

    var organizationToken = ApiToken.Create(owner.Id, organization.Id, "ci", "cvp_abcd", Enumerable.Repeat((byte)1, 32).ToArray(), Now, null);
    var instanceToken = ApiToken.Create(owner.Id, null, "ops", "cvp_efgh", Enumerable.Repeat((byte)2, 32).ToArray(), Now, null);
    context.ApiTokens.AddRange(organizationToken, instanceToken);
    await context.SaveChangesAsync(Ct);

    var instanceTarget = instanceToken.Id.ToString(CultureInfo.InvariantCulture);
    var organizationTarget = organizationToken.Id.ToString(CultureInfo.InvariantCulture);

    async Task RecordAsync(string action, string target, long? organizationId = null)
    {
      var clock = A.Fake<IClock>();
      A.CallTo(() => clock.UtcNow).Returns(Now.AddMinutes(minute++));
      var log = new AuditLog(context, user, new OrganizationContext(), clock);

      if (organizationId is { } id)
      {
        await log.RecordAsync(id, action, target, null, Ct);
      }
      else
      {
        await log.RecordAsync(action, target, null, Ct);
      }
    }

    await RecordAsync("Settings.ReplaceSecret", SettingDefinitions.GitHubClientSecret);
    await RecordAsync("Settings.Reset", SettingDefinitions.GitHubWebhookSecret);
    await RecordAsync("Settings.Reset", SettingDefinitions.UploadsMaxFilesPerUpload);
    await RecordAsync("Settings.Update", "Uploads:MaxFilesPerUpload");
    await RecordAsync("Organization.Admit", "GitHub/Octo-Org");
    await RecordAsync("ApiTokens.Create", organizationTarget);
    await RecordAsync("ApiTokens.Revoke", organizationTarget);
    await RecordAsync("ApiTokens.Create", instanceTarget);
    await RecordAsync("Jobs.Retry", "42");
    await RecordAsync("OrganizationSettings.Update", "org-entry", organization.Id);
    await RecordAsync("Repositories.Activate", "7");

    // The instance token row is renamed in the expectations: its identifier differs between runs.
    await context.AuditEntries.Where(e => e.Target == instanceTarget).ExecuteUpdateAsync(s => s.SetProperty(static e => e.Target, InstanceTokenTarget), Ct);

    return connectionString;
  }

  #endregion
}
