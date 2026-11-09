// ------------------------------------------------------------------------------------------------
// <copyright file="GitCommitRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class GitCommitRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const int Workers = 8;

  private static readonly GitSha Third = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_ShouldStoreTheParentsInOrder(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var merge = new ForgeCommitInfo(Head, [NewHead, Third], "merge", "author", Now);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new GitCommitRepository(writer).AddMissingAsync(seed.FirstRepository, [merge], Now.AddHours(1), Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var saved = (await new GitCommitRepository(reader).FindAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull();

    saved.ParentShas.ShouldBe([NewHead, Third]);
    saved.Message.ShouldBe("merge");
    saved.AuthorLogin.ShouldBe("author");
    saved.CommittedAt.ShouldBe(Now);
    saved.FetchedAt.ShouldBe(Now.AddHours(1));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_SameCommitTwiceInTheList_ShouldAddOneRow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await new GitCommitRepository(context).AddMissingAsync(seed.FirstRepository, [Info(Head), Info(Head)], Now, Ct);

    (await context.GitCommits.CountAsync(Ct)).ShouldBe(1);
  }

  // An immutable cache: a replay changes nothing, and adds only what is missing.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_Twice_ShouldKeepTheFirstRowAndAddTheRest(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var commits = new GitCommitRepository(context);

    await commits.AddMissingAsync(seed.FirstRepository, [Info(Head)], Now, Ct);
    await commits.AddMissingAsync(seed.FirstRepository, [Info(Head), Info(NewHead)], Now.AddHours(1), Ct);

    (await context.GitCommits.CountAsync(Ct)).ShouldBe(2);
    (await commits.FindAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull().FetchedAt.ShouldBe(Now);
    (await commits.FindAsync(seed.FirstRepository, NewHead, Ct)).ShouldNotBeNull().FetchedAt.ShouldBe(Now.AddHours(1));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_Concurrent_ShouldStoreEachCommitOnce(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    ForgeCommitInfo[] commits = [Info(Head), Info(NewHead), Info(Third)];
    var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var workers = Enumerable.Range(0, Workers)
                            .Select(
                              _ => Task.Run(
                                async () =>
                                {
                                  await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
                                  await go.Task.WaitAsync(Ct);

                                  await new GitCommitRepository(context).AddMissingAsync(seed.FirstRepository, commits, Now, Ct);
                                },
                                Ct
                              )
                            )
                            .ToArray();

    go.SetResult();
    await Task.WhenAll(workers).WaitAsync(TimeSpan.FromMinutes(5), Ct);

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await reader.GitCommits.CountAsync(Ct)).ShouldBe(commits.Length);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_SameShaInTwoOrganizations_ShouldKeepBoth(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await AddAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository);
    await AddAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await new GitCommitRepository(first).FindAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull();
    (await new GitCommitRepository(second).FindAsync(seed.SecondRepository, Head, Ct)).ShouldNotBeNull();
  }

  // The primary key does not hold the organization: without the check, the SHA would be taken in the other one.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_RepositoryOfOtherOrganization_ShouldThrowNotFoundAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var exception = await Should.ThrowAsync<NotFoundException>(
        () => new GitCommitRepository(first).AddMissingAsync(seed.SecondRepository, [Info(Head)], Now, Ct)
      );

      exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    }

    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await new GitCommitRepository(second).FindAsync(seed.SecondRepository, Head, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await AddAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new GitCommitRepository(first).FindAsync(seed.SecondRepository, Head, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_Unknown_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new GitCommitRepository(first).FindAsync(seed.FirstRepository, Head, Ct)).ShouldBeNull();
  }

  private static ForgeCommitInfo Info(GitSha sha) => new(sha, [], "message", null, Now);

  private static async Task AddAsync(TwoOrganizationDatabase organizations, long organizationId, long repositoryId)
  {
    await using var context = organizations.ContextOf(organizationId);

    await new GitCommitRepository(context).AddMissingAsync(repositoryId, [Info(Head)], Now, Ct);
  }

  #endregion
}
