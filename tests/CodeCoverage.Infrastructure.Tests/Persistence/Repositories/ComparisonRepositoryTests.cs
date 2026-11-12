// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Comparisons;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class ComparisonRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_ThenFind_ShouldGiveTheComparisonBack(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var stored = Stored(seed.FirstCommit, pullNumber: 12);

    await SaveAsync(seed, seed.Organizations.FirstId, stored);

    var found = (await FindAsync(seed, seed.Organizations.FirstId, seed.FirstCommit, 12)).ShouldNotBeNull();
    found.CommitId.ShouldBe(seed.FirstCommit);
    found.PullNumber.ShouldBe(12);
    found.ForkPointSha.ShouldBe(Head);
    found.ComparedToSha.ShouldBe(NewHead);
    found.HeadRevision.ShouldBe(3);
    found.BaseRevision.ShouldBe(2);
    found.DiffSource.ShouldBe(DiffSource.PullFiles);
    found.ComputedAt.ShouldBe(Now);
    ComparisonDataSerializerTests.ShouldBeTheSame(found.Result, ComparisonSamples.Full);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_ShouldFillTheColumnsReadWithoutDecoding(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 12));

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var row = await context.Comparisons.AsNoTracking().SingleAsync(Ct);
    row.HeadCoverage.ShouldBe(70m);
    row.BaseCoverage.ShouldBe(60m);
    (row.PatchLines, row.PatchHits, row.PatchMisses, row.PatchPartials).ShouldBe((4, 2, 1, 1));
    row.IndirectChangeCount.ShouldBe(3);
    row.DiffIncomplete.ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_PushBuildWithoutBase_ShouldKeepEveryOptionalPartEmpty(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var stored = new CommitComparison(seed.FirstCommit, null, null, null, 1, null, DiffSource.Compare, ComparisonSamples.Empty, Now);

    await SaveAsync(seed, seed.Organizations.FirstId, stored);

    var found = (await FindAsync(seed, seed.Organizations.FirstId, seed.FirstCommit, null)).ShouldNotBeNull();
    found.PullNumber.ShouldBeNull();
    found.ForkPointSha.ShouldBeNull();
    found.ComparedToSha.ShouldBeNull();
    found.BaseRevision.ShouldBeNull();
    found.Result.Base.ShouldBeNull();
  }

  // A commit has one comparison per pull request, and one as a push build.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_SameCommitAsPushBuildAndInTwoPulls_ShouldKeepThree(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: null, headRevision: 1));
    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 12, headRevision: 2));
    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 13, headRevision: 3));

    (await FindAsync(seed, seed.Organizations.FirstId, seed.FirstCommit, null)).ShouldNotBeNull().HeadRevision.ShouldBe(1);
    (await FindAsync(seed, seed.Organizations.FirstId, seed.FirstCommit, 12)).ShouldNotBeNull().HeadRevision.ShouldBe(2);
    (await FindAsync(seed, seed.Organizations.FirstId, seed.FirstCommit, 13)).ShouldNotBeNull().HeadRevision.ShouldBe(3);
    (await FindAsync(seed, seed.Organizations.FirstId, seed.FirstCommit, 14)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_Again_ShouldReplaceTheRow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 12, headRevision: 1));
    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 12, headRevision: 2) with { Result = ComparisonSamples.Empty });

    var found = (await FindAsync(seed, seed.Organizations.FirstId, seed.FirstCommit, 12)).ShouldNotBeNull();
    found.HeadRevision.ShouldBe(2);
    found.Result.Files.ShouldBeEmpty();

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await context.Comparisons.CountAsync(Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 12));
    await SaveAsync(seed, seed.Organizations.SecondId, Stored(seed.SecondCommit, pullNumber: 12, headRevision: 9));

    (await FindAsync(seed, seed.Organizations.SecondId, seed.FirstCommit, 12)).ShouldBeNull();
    (await FindAsync(seed, seed.Organizations.FirstId, seed.SecondCommit, 12)).ShouldBeNull();
    (await FindAsync(seed, seed.Organizations.SecondId, seed.SecondCommit, 12)).ShouldNotBeNull().HeadRevision.ShouldBe(9);
  }

  // The unique index does not hold the organization: a row for the commit of another one would take its key.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_CommitOfAnotherOrganization_ShouldThrowConflictAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    var exception = await Should.ThrowAsync<ConflictException>(() => SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.SecondCommit, pullNumber: 12)));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    (await FindAsync(seed, seed.Organizations.SecondId, seed.SecondCommit, 12)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommits_ShouldGiveEveryComparisonOfTheCommits(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: null, headRevision: 1));
    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 12, headRevision: 2));

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var found = await new ComparisonRepository(context).ListByCommitsAsync([seed.FirstCommit], Ct);

    found.OrderBy(static c => c.HeadRevision).Select(static c => c.PullNumber).ShouldBe([null, 12]);
    ComparisonDataSerializerTests.ShouldBeTheSame(found[0].Result, ComparisonSamples.Full);
    (await new ComparisonRepository(context).ListByCommitsAsync([], Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommits_OtherOrganization_ShouldGiveNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await SaveAsync(seed, seed.Organizations.FirstId, Stored(seed.FirstCommit, pullNumber: 12));
    await SaveAsync(seed, seed.Organizations.SecondId, Stored(seed.SecondCommit, pullNumber: 12, headRevision: 9));

    await using var context = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    var found = await new ComparisonRepository(context).ListByCommitsAsync([seed.FirstCommit, seed.SecondCommit], Ct);

    found.ShouldHaveSingleItem().HeadRevision.ShouldBe(9);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_WithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(() => new ComparisonRepository(context).FindAsync(seed.FirstCommit, null, Ct));
  }

  // Two notifications of the same commit that overlap: one row, the one of the save that ends last.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_TwoAtTheSameTime_ShouldLeaveOneRow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    // Held on its insert: the row does not exist yet for the other save, which inserts too and commits first.
    var holding = new HoldingCommandInterceptor(static sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                                                             && sql.Contains("Comparisons", StringComparison.Ordinal));

    var holder = SaveInItsOwnContextAsync(seed, provider, holding, headRevision: 1);
    await holding.Reached.Task.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    await SaveInItsOwnContextAsync(seed, provider, null, headRevision: 2);

    holding.Gate.SetResult();
    await holder.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var row = await context.Comparisons.AsNoTracking().SingleAsync(Ct);
    // The held save lost the insert, found the row and replaced it.
    row.HeadRevision.ShouldBe(1);
  }

  private static CommitComparison Stored(long commitId, int? pullNumber, int headRevision = 3) =>
    new(commitId, pullNumber, Head, NewHead, headRevision, 2, DiffSource.PullFiles, ComparisonSamples.Full, Now);

  private static async Task SaveAsync(CommitSeed seed, long organizationId, CommitComparison comparison)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);

    await new ComparisonRepository(context).SaveAsync(comparison, Ct);
  }

  private static async Task<CommitComparison?> FindAsync(CommitSeed seed, long organizationId, long commitId, int? pullNumber)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);

    return await new ComparisonRepository(context).FindAsync(commitId, pullNumber, Ct);
  }

  private static async Task SaveInItsOwnContextAsync(CommitSeed seed, DatabaseProvider provider, HoldingCommandInterceptor? interceptor, int headRevision)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, seed.Organizations.ConnectionString);

    if (interceptor is not null)
    {
      options.AddInterceptors(interceptor);
    }

    var organization = new OrganizationContext();
    organization.Enter(seed.Organizations.FirstId);

    await using var context = new CodeCoverageDbContext(options.Options, organization);

    await new ComparisonRepository(context).SaveAsync(Stored(seed.FirstCommit, pullNumber: 12, headRevision), Ct);
  }

  private async Task<CommitSeed> SeededAsync(DatabaseProvider provider)
  {
    var repositories = await AddRepositoriesAsync(database, provider, Ct);

    await using var first = repositories.Organizations.ContextOf(repositories.Organizations.FirstId);
    await using var second = repositories.Organizations.ContextOf(repositories.Organizations.SecondId);
    var firstCommit = await new CommitRepository(first).GetOrCreateAsync(repositories.FirstRepository, Head, Now, Ct);
    var secondCommit = await new CommitRepository(second).GetOrCreateAsync(repositories.SecondRepository, Head, Now, Ct);

    return new CommitSeed(repositories.Organizations, repositories.FirstRepository, firstCommit.Id, secondCommit.Id);
  }

  #endregion
}