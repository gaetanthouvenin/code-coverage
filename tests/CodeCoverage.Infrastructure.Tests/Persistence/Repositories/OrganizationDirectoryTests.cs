// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationDirectoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

[Collection(DatabaseCollection.Name)]
public sealed class OrganizationDirectoryTests(DatabaseFixture database)
{
  #region Fields

  private const string FirstAccount = "1001";

  private const string SecondAccount = "2002";

  // Never opened: reading the model does not reach the server.
  private const string UnusedConnection = "Server=localhost;Database=unused";

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByLogin_ShouldBeCaseInsensitive(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var created = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "Octo-Org", OrganizationAccountType.Organization, Ct);

    var found = await directory.FindByLoginAsync(Forge.GitHub,"OCTO-org", Ct);

    found.ShouldNotBeNull().Id.ShouldBe(created.Id);
    found.Login.ShouldBe("Octo-Org");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByLogin_UnknownLogin_ShouldReturnNull(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);

    (await Directory(context).FindByLoginAsync(Forge.GitHub,"nobody", Ct)).ShouldBeNull();
  }

  // The acceptance of the micro-feature: a tombstone must never resolve, or a crafted owner would
  // reach the row of the account that lost the login.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByLogin_TombstonedLogin_ShouldReturnNull(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);
    await directory.UpsertFromForgeAsync(Forge.GitHub,SecondAccount, "octo", OrganizationAccountType.Organization, Ct);

    (await directory.FindByLoginAsync(Forge.GitHub,$"~stale-{FirstAccount}", Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByLogin_ReclaimedLogin_ShouldReturnNewAccount(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);
    await directory.UpsertFromForgeAsync(Forge.GitHub,SecondAccount, "OCTO", OrganizationAccountType.User, Ct);

    var found = await directory.FindByLoginAsync(Forge.GitHub,"octo", Ct);

    found.ShouldNotBeNull().ExternalAccountId.ShouldBe(SecondAccount);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByLogin_SameLoginOnTwoForges_ShouldReturnTheRowOfThatForge(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var onGitHub = await directory.UpsertFromForgeAsync(Forge.GitHub, FirstAccount, "octo", OrganizationAccountType.Organization, Ct);
    var onAzure = await directory.UpsertFromForgeAsync(Forge.AzureDevOps, SecondAccount, "octo", OrganizationAccountType.Organization, Ct);

    (await directory.FindByLoginAsync(Forge.GitHub, "octo", Ct)).ShouldNotBeNull().Id.ShouldBe(onGitHub.Id);
    (await directory.FindByLoginAsync(Forge.AzureDevOps, "octo", Ct)).ShouldNotBeNull().Id.ShouldBe(onAzure.Id);
    (await directory.FindByLoginAsync(Forge.Bitbucket, "octo", Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_SameLoginOnOtherForge_ShouldNotTombstone(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    await directory.UpsertFromForgeAsync(Forge.GitHub, FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    await directory.UpsertFromForgeAsync(Forge.Bitbucket, SecondAccount, "octo", OrganizationAccountType.Organization, Ct);

    (await directory.FindByAccountIdAsync(Forge.GitHub, FirstAccount, Ct)).ShouldNotBeNull().Login.ShouldBe("octo");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_SameLoginOnGitLabAndGitHub_ShouldKeepBoth(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var onGitHub = await directory.UpsertFromForgeAsync(Forge.GitHub, FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    var onGitLab = await directory.UpsertFromForgeAsync(Forge.GitLab, SecondAccount, "octo", OrganizationAccountType.Organization, Ct);

    onGitLab.Id.ShouldNotBe(onGitHub.Id);
    (await directory.FindByLoginAsync(Forge.GitHub, "octo", Ct)).ShouldNotBeNull().Id.ShouldBe(onGitHub.Id);
    (await directory.FindByLoginAsync(Forge.GitLab, "octo", Ct)).ShouldNotBeNull().Id.ShouldBe(onGitLab.Id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByLogin_DottedGitLabPath_ShouldBeFound(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var created = await directory.UpsertFromForgeAsync(Forge.GitLab, FirstAccount, "Octo.Group", OrganizationAccountType.Organization, Ct);

    (await directory.FindByLoginAsync(Forge.GitLab, "octo.group", Ct)).ShouldNotBeNull().Id.ShouldBe(created.Id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_LongestLogin_ShouldBeSaved(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var login = new string('a', Organization.LoginMaxLength);

    var created = await Directory(context).UpsertFromForgeAsync(Forge.GitLab, FirstAccount, login, OrganizationAccountType.Organization, Ct);

    created.Login.Length.ShouldBe(Organization.LoginMaxLength);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_SameAccountIdOnOtherForge_ShouldCreateARow(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var onGitHub = await directory.UpsertFromForgeAsync(Forge.GitHub, FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    var onAzure = await directory.UpsertFromForgeAsync(Forge.AzureDevOps, FirstAccount, "other", OrganizationAccountType.Organization, Ct);

    onAzure.Id.ShouldNotBe(onGitHub.Id);
    (await context.Organizations.CountAsync(Ct)).ShouldBe(2);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByAccountId_ShouldReturnTheRowOfTheAccount(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    (await directory.FindByAccountIdAsync(Forge.GitHub,FirstAccount, Ct)).ShouldNotBeNull().Login.ShouldBe("octo");
    (await directory.FindByAccountIdAsync(Forge.GitHub,SecondAccount, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindCurrent_WhenEntered_ShouldReturnTheOrganization(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var current = new OrganizationContext();
    var directory = Directory(context, current);
    await directory.UpsertFromForgeAsync(Forge.GitHub, SecondAccount, "other", OrganizationAccountType.Organization, Ct);
    var created = await directory.UpsertFromForgeAsync(Forge.GitHub, FirstAccount, "octo", OrganizationAccountType.Organization, Ct);
    current.Enter(created.Id);

    var found = await directory.FindCurrentAsync(Ct);

    found.ShouldNotBeNull().Id.ShouldBe(created.Id);
  }

  [Fact]
  public async Task FindCurrent_WhenUnset_ShouldThrow()
  {
    await using var context = DatabaseFixture.CreateContext(DatabaseProvider.PostgreSql, UnusedConnection);

    await Should.ThrowAsync<InvalidOperationException>(() => Directory(context).FindCurrentAsync(Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListActive_ShouldReturnOnlyActive(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var active = Organization.CreatePending(Forge.GitHub, FirstAccount, "active-org", OrganizationAccountType.Organization, Now);
    active.Admit("admin-user", Now);
    var suspended = Organization.CreatePending(Forge.GitHub, SecondAccount, "suspended-org", OrganizationAccountType.Organization, Now);
    suspended.Suspend(SuspensionReason.Administrator, Now);
    var pending = Organization.CreatePending(Forge.GitHub, "3003", "pending-org", OrganizationAccountType.Organization, Now);
    context.Organizations.AddRange(active, suspended, pending);
    await context.SaveChangesAsync(Ct);

    var listed = await Directory(context).ListActiveAsync(Ct);

    listed.ShouldHaveSingleItem().ExternalAccountId.ShouldBe(FirstAccount);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_NewAccount_ShouldBePending(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);

    var created = await Directory(context).UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "Octo", OrganizationAccountType.User, Ct);

    created.Id.ShouldBeGreaterThan(0);
    created.State.ShouldBe(OrganizationState.Pending);
    created.NormalizedLogin.ShouldBe("octo");
    created.AccountType.ShouldBe(OrganizationAccountType.User);
    created.CreatedAt.ShouldBe(Now);
    context.ChangeTracker.Entries<Organization>().ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_LoginHeldByOtherAccount_ShouldTombstoneOldRow(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var old = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    var created = await directory.UpsertFromForgeAsync(Forge.GitHub,SecondAccount, "Octo", OrganizationAccountType.Organization, Ct);

    var tombstoned = await directory.FindByAccountIdAsync(Forge.GitHub,FirstAccount, Ct);
    tombstoned.ShouldNotBeNull().Id.ShouldBe(old.Id);
    tombstoned.Login.ShouldBe($"~stale-{FirstAccount}");
    tombstoned.NormalizedLogin.ShouldBe($"~stale-{FirstAccount}");
    created.Id.ShouldNotBe(old.Id);
    created.Login.ShouldBe("Octo");
  }

  // The account that lost its login reports a new one at the next sync: its row keeps its identity.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_TombstonedAccountNewLogin_ShouldRestoreIt(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var old = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);
    await directory.UpsertFromForgeAsync(Forge.GitHub,SecondAccount, "octo", OrganizationAccountType.Organization, Ct);

    var restored = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo-renamed", OrganizationAccountType.Organization, Ct);

    restored.Id.ShouldBe(old.Id);
    restored.Login.ShouldBe("octo-renamed");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_SameAccountNewLogin_ShouldKeepId(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var first = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    var renamed = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo-renamed", OrganizationAccountType.User, Ct);

    renamed.Id.ShouldBe(first.Id);
    renamed.ExternalAccountId.ShouldBe(FirstAccount);
    renamed.Login.ShouldBe("octo-renamed");
    renamed.AccountType.ShouldBe(OrganizationAccountType.User);
    (await directory.FindByLoginAsync(Forge.GitHub,"octo", Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_NothingChanged_ShouldNotWriteTheRow(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var directory = Directory(context);
    var first = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    var second = await directory.UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);

    second.Version.ShouldBe(first.Version);
  }

  [Fact]
  public async Task Upsert_MalformedLogin_ShouldThrow()
  {
    await using var context = DatabaseFixture.CreateContext(DatabaseProvider.PostgreSql, UnusedConnection);

    await Should.ThrowAsync<ArgumentException>(
      () => Directory(context).UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "~stale-1", OrganizationAccountType.User, Ct)
    );
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_ConcurrentSameAccount_ShouldKeepOneRow(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    var contexts = Enumerable.Range(0, 4).Select(_ => DatabaseFixture.CreateContext(provider, connection)).ToList();

    try
    {
      var upserts = contexts.Select(
        static c => Directory(c).UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct)
      );

      var results = await Task.WhenAll(upserts);

      results.Select(static o => o.Id).Distinct().ShouldHaveSingleItem();
      (await contexts[0].Organizations.CountAsync(Ct)).ShouldBe(1);
    }
    finally
    {
      foreach (var context in contexts)
      {
        await context.DisposeAsync();
      }
    }
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_StaleVersion_ShouldThrowConcurrencyException(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var seeding = DatabaseFixture.CreateContext(provider, connection);
    await Directory(seeding).UpsertFromForgeAsync(Forge.GitHub,FirstAccount, "octo", OrganizationAccountType.Organization, Ct);
    await using var first = DatabaseFixture.CreateContext(provider, connection);
    await using var second = DatabaseFixture.CreateContext(provider, connection);
    var winner = await first.Organizations.SingleAsync(Ct);
    var loser = await second.Organizations.SingleAsync(Ct);
    winner.Rename("winner", Now);
    await first.SaveChangesAsync(Ct);

    loser.Rename("loser", Now);

    await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
  }

  // "No business data": a column added to this global table has to be justified here.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_Organizations_ShouldHoldOnlyDirectoryColumns(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, UnusedConnection);

    var columns = context.Model.FindEntityType(typeof(Organization)).ShouldNotBeNull()
                         .GetProperties().Select(static p => p.GetColumnName());

    columns.ShouldBe(
      [
        "AccountType", "AdmittedBy", "CreatedAt", "ExternalAccountId", "Forge", "Id", "Login", "NormalizedLogin",
        "State", "SuspensionReason", "UpdatedAt", "Version",
      ],
      ignoreOrder: true
    );
  }

  private static OrganizationDirectory Directory(CodeCoverageDbContext context, ICurrentOrganization? current = null)
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    return new OrganizationDirectory(context, clock, current ?? new OrganizationContext());
  }

  private async Task<string> MigratedDatabaseAsync(DatabaseProvider provider)
  {
    var connection = await database.NewDatabaseAsync(provider, Ct);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    await context.Database.MigrateAsync(Ct);

    return connection;
  }

  #endregion
}
