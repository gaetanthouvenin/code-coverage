// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryFlagsBackfillTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

// The migration that adds RepositoryFlags runs on databases that already received uploads: the flags of their sessions
// are the flags their repositories know.
[Collection(DatabaseCollection.Name)]
public sealed class RepositoryFlagsBackfillTests(DatabaseFixture database)
{
  #region Fields

  private const string Migration = "AddRepositoryFlags";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Up_DatabaseThatAlreadyReceivedUploads_ShouldTakeTheFlagsOfTheSessions(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var organizations = seed.Organizations;
    long otherRepository;

    await using (var seeding = organizations.ContextOf(organizations.FirstId))
    {
      otherRepository = await AddRepositoryAsync(seeding, FirstInstallation, "77", "first-org/other", Ct);
    }

    await MigrateAsync(organizations, Previous(organizations));

    // First organization, first repository: two commits. The flags Unit and unit only differ by their case. `declared`
    // is in the configuration and has totals, but no session ever carried it: the repository does not know it.
    var early = Now.AddDays(-2);
    var firstCommit = await AddCommitAsync(organizations, organizations.FirstId, seed.FirstRepository, Head, early);
    await AddSessionsAsync(organizations, organizations.FirstId, firstCommit, ["unit", "Unit"], ["back", "unit"], []);
    await AddTotalsAsync(organizations, organizations.FirstId, firstCommit, CommitFlagTotals.ProjectFlag, "unit", "Unit", "back", "declared");

    var secondCommit = await AddCommitAsync(organizations, organizations.FirstId, seed.FirstRepository, NewHead, Now);
    await AddSessionsAsync(organizations, organizations.FirstId, secondCommit, ["unit"], ["e2e"]);
    await AddTotalsAsync(organizations, organizations.FirstId, secondCommit, CommitFlagTotals.ProjectFlag, "unit", "e2e");

    // Another repository of the same organization, and the repository of the other organization, with the same flag.
    var otherCommit = await AddCommitAsync(organizations, organizations.FirstId, otherRepository, Head, Now);
    await AddSessionsAsync(organizations, organizations.FirstId, otherCommit, ["unit", "it's \"quoted\""]);

    var foreignCommit = await AddCommitAsync(organizations, organizations.SecondId, seed.SecondRepository, Head, Now);
    await AddSessionsAsync(organizations, organizations.SecondId, foreignCommit, ["unit", "second"]);

    await MigrateAsync(organizations, null);

    (await ListAsync(organizations, organizations.FirstId, seed.FirstRepository)).ShouldBe(["Unit", "back", "e2e", "unit"]);
    (await ListAsync(organizations, organizations.FirstId, otherRepository)).ShouldBe(["it's \"quoted\"", "unit"]);
    (await ListAsync(organizations, organizations.SecondId, seed.SecondRepository)).ShouldBe(["second", "unit"]);

    // Each row is in the organization of its repository: the other one reads nothing of it.
    (await ListAsync(organizations, organizations.SecondId, seed.FirstRepository)).ShouldBeEmpty();
    (await ListAsync(organizations, organizations.FirstId, seed.SecondRepository)).ShouldBeEmpty();

    await using var check = organizations.ContextWithoutOrganization();
    (await check.RepositoryFlags.IgnoreQueryFilters().CountAsync(Ct)).ShouldBe(8);

    // A flag is known since the first commit that carried it.
    await using var first = organizations.ContextOf(organizations.FirstId);
    var since = await first.RepositoryFlags
                           .AsNoTracking()
                           .Where(f => f.RepositoryId == seed.FirstRepository)
                           .ToDictionaryAsync(static f => f.Flag, static f => f.CreatedAt, Ct);
    since["unit"].ShouldBe(early);
    since["e2e"].ShouldBe(Now);

    // The flags taken over are flags as any other: a finalization adds to them, and records none twice.
    await new RepositoryFlagRepository(first).AddMissingAsync(seed.FirstRepository, ["unit", "new"], Now, Ct);
    (await ListAsync(organizations, organizations.FirstId, seed.FirstRepository)).ShouldBe(["Unit", "back", "e2e", "new", "unit"]);
  }

  // A database without any session: the table is created empty.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Up_DatabaseWithoutAnySession_ShouldCreateAnEmptyTable(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await MigrateAsync(seed.Organizations, Previous(seed.Organizations));
    await MigrateAsync(seed.Organizations, null);

    await using var check = seed.Organizations.ContextWithoutOrganization();
    (await check.RepositoryFlags.IgnoreQueryFilters().CountAsync(Ct)).ShouldBe(0);
    (await check.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
  }

  // The migration applied just before the one that adds the table.
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

  private static async Task<long> AddCommitAsync(TwoOrganizationDatabase organizations, long organizationId, long repositoryId, Domain.Git.GitSha sha, DateTimeOffset at)
  {
    await using var context = organizations.ContextOf(organizationId);

    var commit = await new CommitRepository(context).GetOrCreateAsync(repositoryId, sha, at.AddMinutes(-5), Ct);

    // Finalized: its totals can be written, and the moment of its finalization is when its flags were first seen.
    commit.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, "source", at);
    await context.SaveChangesAsync(Ct);

    return commit.Id;
  }

  private static async Task AddSessionsAsync(TwoOrganizationDatabase organizations, long organizationId, long commitId, params string[][] flags)
  {
    await using var context = organizations.ContextOf(organizationId);

    context.CommitSessions.AddRange(flags.Select((list, index) => Session.FromUpload(commitId, index + 1, index + 1, list, null, null, null, default)));
    await context.SaveChangesAsync(Ct);
  }

  private static async Task AddTotalsAsync(TwoOrganizationDatabase organizations, long organizationId, long commitId, params string[] flags)
  {
    await using var context = organizations.ContextOf(organizationId);
    var commit = await context.Commits.SingleAsync(c => c.Id == commitId, Ct);

    context.CommitFlagTotals.AddRange(flags.Select(flag => CommitFlagTotals.Create(commit, flag, default, false, null)));
    await context.SaveChangesAsync(Ct);
  }

  private static async Task<IReadOnlyList<string>> ListAsync(TwoOrganizationDatabase organizations, long organizationId, long repositoryId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await new RepositoryFlagRepository(context).ListAsync(repositoryId, Ct);
  }

  #endregion
}
