// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuotasMigrationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

// The migration that turns GitHubQuotas into ForgeQuotas runs on databases that already recorded GitHub quotas: each one
// is read back on the forge GitHub, unchanged. Going back keeps the GitHub quotas the former table can hold.
[Collection(DatabaseCollection.Name)]
public sealed class ForgeQuotasMigrationTests(DatabaseFixture database)
{
  #region Fields

  private const string Migration = "RenameGitHubQuotasToForgeQuotas";

  private static readonly DateTimeOffset ResetAt = new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero);

  private static readonly DateTimeOffset ObservedAt = new(2026, 11, 20, 17, 30, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Up_ExistingGitHubQuotas_ShouldReadThemOnTheForgeGitHub(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await MigrateAsync(organizations, Previous(organizations));

    await using (var seeding = organizations.ContextWithoutOrganization())
    {
      await seeding.Database.ExecuteSqlAsync(
        $"""INSERT INTO "GitHubQuotas" ("Installation", "Remaining", "Limit", "ResetAt", "ObservedAt") VALUES ({"42"}, {4321L}, {5000L}, {ResetAt}, {ObservedAt})""",
        Ct);
      await seeding.Database.ExecuteSqlAsync(
        $"""INSERT INTO "GitHubQuotas" ("Installation", "Remaining", "ObservedAt") VALUES ({ForgeQuota.AppConnection}, {12L}, {ObservedAt})""",
        Ct);
    }

    await MigrateAsync(organizations, null);

    await using var check = organizations.ContextWithoutOrganization();
    var quotas = await check.ForgeQuotas.AsNoTracking().OrderBy(static q => q.Connection).ToListAsync(Ct);

    quotas.Count.ShouldBe(2);

    var installation = quotas[0];
    installation.Forge.ShouldBe(Forge.GitHub);
    installation.Connection.ShouldBe("42");
    installation.Remaining.ShouldBe(4321);
    installation.Limit.ShouldBe(5000);
    installation.ResetAt.ShouldBe(ResetAt);
    installation.ObservedAt.ShouldBe(ObservedAt);

    var app = quotas[1];
    app.Forge.ShouldBe(Forge.GitHub);
    app.Connection.ShouldBe(ForgeQuota.AppConnection);
    app.Remaining.ShouldBe(12);
    app.Limit.ShouldBeNull();
    app.ResetAt.ShouldBeNull();

    (await check.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Down_QuotasOfSeveralForges_ShouldKeepOnlyTheGitHubQuotasTheFormerTableCanHold(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var longConnection = new string('c', 40);

    await using (var seeding = organizations.ContextWithoutOrganization())
    {
      seeding.ForgeQuotas.AddRange(
        ForgeQuota.Create(Forge.GitHub, "42", 4321, 5000, ResetAt, ObservedAt),
        ForgeQuota.Create(Forge.Bitbucket, "42", 1, 1000, null, ObservedAt),
        ForgeQuota.Create(Forge.GitHub, longConnection, 2, 5000, null, ObservedAt));
      await seeding.SaveChangesAsync(Ct);
    }

    await MigrateAsync(organizations, Previous(organizations));

    await using (var check = organizations.ContextWithoutOrganization())
    {
      var installations = await check.Database
                                     .SqlQuery<string>($"""SELECT "Installation" AS "Value" FROM "GitHubQuotas" """)
                                     .ToListAsync(Ct);
      var remaining = await check.Database
                                 .SqlQuery<long>($"""SELECT "Remaining" AS "Value" FROM "GitHubQuotas" """)
                                 .ToListAsync(Ct);

      installations.ShouldBe(["42"]);
      remaining.ShouldBe([4321L]);
    }

    // Up again: the quota kept is read back on the forge GitHub.
    await MigrateAsync(organizations, null);

    await using var after = organizations.ContextWithoutOrganization();
    var quota = await after.ForgeQuotas.AsNoTracking().SingleAsync(Ct);

    quota.Forge.ShouldBe(Forge.GitHub);
    quota.Connection.ShouldBe("42");
  }

  // The migration applied just before the one that renames the table.
  private static string Previous(TwoOrganizationDatabase organizations)
  {
    using var context = organizations.ContextWithoutOrganization();
    var migrations = context.Database.GetMigrations().ToList();
    var index = migrations.FindIndex(static id => id.EndsWith("_" + Migration, StringComparison.Ordinal));

    index.ShouldBeGreaterThan(0);

    return migrations[index - 1];
  }

  private static async Task MigrateAsync(TwoOrganizationDatabase organizations, string? target)
  {
    await using var context = organizations.ContextWithoutOrganization();

    await context.GetService<IMigrator>().MigrateAsync(target, Ct);
  }

  #endregion
}
