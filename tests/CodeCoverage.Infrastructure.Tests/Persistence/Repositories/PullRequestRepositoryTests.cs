// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class PullRequestRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private static readonly GitSha Base = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_New_ShouldStoreThePullRequest(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new PullRequestRepository(writer).UpsertAsync(seed.FirstRepository, Info(7), Now, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var saved = await reader.PullRequests.AsNoTracking().SingleAsync(Ct);

    saved.Id.ShouldBePositive();
    saved.Number.ShouldBe(7);
    saved.State.ShouldBe(PullRequestState.Open);
    saved.Title.ShouldBe("title");
    saved.HeadSha.ShouldBe(Head);
    saved.BaseShaGitHub.ShouldBe(Base);
    saved.Version.ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_Known_ShouldApplyTheChangesAndIncrementTheVersion(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await UpsertAsync(seed, Info(7));

    var updated = await UpsertAsync(seed, Info(7) with { HeadSha = NewHead, State = ForgePullState.Merged });

    updated.HeadSha.ShouldBe(NewHead);
    updated.State.ShouldBe(PullRequestState.Merged);
    updated.Version.ShouldBe(1);

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await reader.PullRequests.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_RepositoryOfOtherOrganization_ShouldThrowNotFound(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    var exception = await Should.ThrowAsync<NotFoundException>(() => new PullRequestRepository(other).UpsertAsync(seed.FirstRepository, Info(7), Now, Ct));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  // Each writer reads the row before the other commits: the second update refuses instead of overwriting.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_StaleVersion_ShouldThrowConflict(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await UpsertAsync(seed, Info(7));

    await using var stale = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var loser = await stale.PullRequests.SingleAsync(Ct);
    await UpsertAsync(seed, Info(7) with { Title = "first" });

    loser.Close(merged: true, Now);

    var exception = await Should.ThrowAsync<ConflictException>(() => stale.SaveChangesAsync(Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindOpenByHeadSha_ShouldSkipClosedAndKeepTheHighestNumber(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await UpsertAsync(seed, Info(7));
    await UpsertAsync(seed, Info(9));
    await UpsertAsync(seed, Info(11) with { State = ForgePullState.Closed });

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new PullRequestRepository(context).FindOpenByHeadShaAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull().Number.ShouldBe(9);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindOpenByHeadSha_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await UpsertAsync(seed, Info(7));

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new PullRequestRepository(other).FindOpenByHeadShaAsync(seed.FirstRepository, Head, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListOpenByForkPoint_ShouldKeepOpenPullRequestsOfTheOrganization(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var context = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var repository = new PullRequestRepository(context);
      await SetForkPointAsync(context, await repository.UpsertAsync(seed.FirstRepository, Info(7), Now, Ct));
      await SetForkPointAsync(context, await repository.UpsertAsync(seed.FirstRepository, Info(8) with { State = ForgePullState.Closed }, Now, Ct));
    }

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      (await new PullRequestRepository(first).ListOpenByForkPointAsync(seed.FirstRepository, Base, Ct)).ShouldHaveSingleItem().Number.ShouldBe(7);
    }

    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new PullRequestRepository(second).ListOpenByForkPointAsync(seed.FirstRepository, Base, Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_Concurrent_ShouldCreateOnce(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => UpsertAsync(seed, Info(7))));

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await reader.PullRequests.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  private static ForgePullInfo Info(int number) => new(number, ForgePullState.Open, "title", "octocat", Head, "feature", "main", Base);

  // The resolution of the fork point comes with BaseResolver (M2.10).
  private static async Task SetForkPointAsync(CodeCoverageDbContext context, PullRequest pull)
  {
    context.Entry(pull).Property(static p => p.ForkPointSha).CurrentValue = Base;
    await context.SaveChangesAsync(Ct);
  }

  private static async Task<PullRequest> UpsertAsync(BranchSeed seed, ForgePullInfo pull)
  {
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    return await new PullRequestRepository(context).UpsertAsync(seed.FirstRepository, pull, Now, Ct);
  }

  #endregion
}
