// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySummaryReadsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// The grouped reads of the list of the repositories (M3.20), each against two organizations on both engines: the
// identifiers of the other organization are asked too, and nothing of it is read.
[Collection(DatabaseCollection.Name)]
public sealed class RepositorySummaryReadsTests(DatabaseFixture database)
{
  #region Fields

  private static readonly CoverageTotals Totals = new(8, 2, 0, 0, 0);

  private readonly TestMeterFactory _meterFactory = new();

  private readonly JobWakeUpSignal _wakeUpSignal = new(TimeProvider.System);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Only the head the default branch covers is read: a covered feature branch or another organization is not.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListCoveredDefaultHeads_ShouldReadTheCoveredHeadOfTheDefaultBranchOfThisOrganization(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var mainHead = await AddCommitAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository, '1', "main", Now.AddHours(1), cover: true);
    await AddCommitAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository, '2', "feature", Now.AddHours(2), cover: true);
    await AddCommitAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository, '3', "main", Now.AddHours(1), cover: true);

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var heads = await new CommitRepository(reader).ListCoveredDefaultHeadsAsync([seed.FirstRepository, seed.SecondRepository], Ct);

    var head = heads.ShouldHaveSingleItem();
    head.Id.ShouldBe(mainHead);
    head.Coverage.ShouldBe(80m);
    reader.ChangeTracker.Entries().ShouldBeEmpty();
    (await new CommitRepository(reader).ListCoveredDefaultHeadsAsync([], Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByRepositories_ShouldReadTheFlagsOfThisOrganizationInOrdinalOrder(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new RepositoryFlagRepository(first).AddMissingAsync(seed.FirstRepository, ["unit", "Api", "api"], Now, Ct);
    }

    await using (var second = seed.Organizations.ContextOf(seed.Organizations.SecondId))
    {
      await new RepositoryFlagRepository(second).AddMissingAsync(seed.SecondRepository, ["e2e"], Now, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var flags = await new RepositoryFlagRepository(reader).ListByRepositoriesAsync([seed.FirstRepository, seed.SecondRepository], Ct);

    flags.Keys.ShouldBe([seed.FirstRepository]);
    flags[seed.FirstRepository].ShouldBe(["Api", "api", "unit"]);
  }

  // The project totals of the default branch within the range, the most recent first under the limit.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListDefaultBranchHistory_ShouldReadTheProjectOfTheDefaultBranchWithinTheRange(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    var older = await AddCommitAsync(seed.Organizations, first, seed.FirstRepository, '1', "main", Now.AddHours(1), cover: false);
    var newer = await AddCommitAsync(seed.Organizations, first, seed.FirstRepository, '2', "main", Now.AddHours(5), cover: false);
    await AddCommitAsync(seed.Organizations, first, seed.FirstRepository, '3', "feature", Now.AddHours(3), cover: false);
    await AddCommitAsync(seed.Organizations, first, seed.FirstRepository, '4', "main", Now.AddDays(3), cover: false);
    await AddCommitAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository, '5', "main", Now.AddHours(2), cover: false);

    await using var reader = seed.Organizations.ContextOf(first);
    var reports = Reports(reader, first);
    var history = await reports.ListDefaultBranchHistoryAsync([seed.FirstRepository, seed.SecondRepository], Now, Now.AddDays(1), 10, Ct);

    history.Select(static t => (t.CommitId, t.Flag)).ShouldBe([(newer, CommitFlagTotals.ProjectFlag), (older, CommitFlagTotals.ProjectFlag)]);
    history[0].Coverage.ShouldBe(80m);
    (await reports.ListDefaultBranchHistoryAsync([seed.FirstRepository], Now, Now.AddDays(1), 1, Ct)).ShouldHaveSingleItem().CommitId.ShouldBe(newer);
  }

  // A commit completed on a branch, with the project and one flag; the branch covers it when asked.
  private static async Task<long> AddCommitAsync(
    TwoOrganizationDatabase organizations,
    long organizationId,
    long repositoryId,
    char sha,
    string branch,
    DateTimeOffset completedAt,
    bool cover)
  {
    await using var context = organizations.ContextOf(organizationId);
    var gitSha = GitSha.Parse(new string(sha, GitSha.Length));
    var commit = await new CommitRepository(context).GetOrCreateAsync(repositoryId, gitSha, Now, Ct);

    commit.MarkPending(branch, null, completedAt);
    commit.RecordFinalization(CommitState.Complete, 1, Totals, 1, 1, CarryforwardStatus.None, "default", completedAt);
    await context.SaveChangesAsync(Ct);

    context.CommitFlagTotals.Add(CommitFlagTotals.Create(commit, CommitFlagTotals.ProjectFlag, Totals, false, null));
    context.CommitFlagTotals.Add(CommitFlagTotals.Create(commit, "unit", Totals, false, null));

    if (cover)
    {
      var row = Branch.Create(repositoryId, branch, gitSha, completedAt);
      row.Cover(commit.Id, completedAt);
      context.Branches.Add(row);
    }

    await context.SaveChangesAsync(Ct);

    return commit.Id;
  }

  private CommitReportRepository Reports(CodeCoverageDbContext context, long organizationId)
  {
    var organization = new OrganizationContext();
    organization.Enter(organizationId);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var queue = new SqlJobQueue(
      context,
      organization,
      clock,
      new ConfigurationBuilder().Build(),
      JobClaimDialects.For(context),
      _wakeUpSignal,
      new CodeCoverageMeter(_meterFactory)
    );

    return new CommitReportRepository(context, queue, _wakeUpSignal);
  }

  #endregion
}
