// ------------------------------------------------------------------------------------------------
// <copyright file="CommitAncestryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Services;

public sealed class CommitAncestryTests
{
  #region Fields

  private const long RepositoryId = 5;

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "101", "1");

  private readonly Dictionary<GitSha, GitCommit> _cache = [];

  private readonly IGitCommitRepository _commits = A.Fake<IGitCommitRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly CommitAncestry _ancestry;

  #endregion

  #region Ctors

  public CommitAncestryTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    A.CallTo(() => _commits.FindAsync(RepositoryId, A<GitSha>._, A<CancellationToken>._))
     .ReturnsLazily((long _, GitSha sha, CancellationToken _) => _cache.GetValueOrDefault(sha));

    A.CallTo(() => _commits.AddMissingAsync(RepositoryId, A<IReadOnlyCollection<ForgeCommitInfo>>._, Now, A<CancellationToken>._))
     .Invokes((long _, IReadOnlyCollection<ForgeCommitInfo> page, DateTimeOffset _, CancellationToken _) =>
     {
       foreach (var commit in page)
       {
         _cache[commit.Sha] = GitCommit.Create(RepositoryId, commit, Now);
       }
     });

    _ancestry = new CommitAncestry(_commits, gateways, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetChain_WhenCached_ShouldNotCallGitHub()
  {
    Cache(History(0, 10));

    var chain = await _ancestry.GetFirstParentChainAsync(RepositoryId, Repository, Sha(9), 3, Ct);

    chain.Select(static c => c.Sha).ShouldBe([Sha(8), Sha(7), Sha(6)]);
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  // The commit and 49 ancestors come with the first call, and the 50th ancestor needs the second.
  [Fact]
  public async Task GetChain_ShouldFetch50PerCall()
  {
    var history = History(0, 200);
    A.CallTo(() => _gateway.ListCommitsAsync(Repository, A<GitSha>._, CommitAncestry.PageSize, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef _, GitSha sha, int perPage, CancellationToken _) =>
       (IReadOnlyList<ForgeCommitInfo>)[.. history.SkipWhile(c => c.Sha != sha).Take(perPage)]);

    var chain = await _ancestry.GetFirstParentChainAsync(RepositoryId, Repository, Sha(199), 50, Ct);

    chain.Count.ShouldBe(50);
    chain[^1].Sha.ShouldBe(Sha(149));
    A.CallTo(() => _gateway.ListCommitsAsync(Repository, A<GitSha>._, 50, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public async Task GetChain_ShouldFollowTheFirstParentOfAMerge()
  {
    var feature = Commit(100, []);
    var main = Commit(1, []);
    var merge = new ForgeCommitInfo(Sha(2), [main.Sha, feature.Sha], "merge", null, Now);
    Cache([merge, main, feature]);

    var chain = await _ancestry.GetFirstParentChainAsync(RepositoryId, Repository, Sha(2), 5, Ct);

    chain.Select(static c => c.Sha).ShouldBe([main.Sha]);
  }

  [Fact]
  public async Task GetChain_AtTheRootCommit_ShouldStop()
  {
    Cache(History(0, 3));

    var chain = await _ancestry.GetFirstParentChainAsync(RepositoryId, Repository, Sha(2), 10, Ct);

    chain.Count.ShouldBe(2);
  }

  [Fact]
  public async Task GetChain_ForgeDoesNotHaveTheCommit_ShouldReturnNothing()
  {
    A.CallTo(() => _gateway.ListCommitsAsync(Repository, Sha(1), 50, A<CancellationToken>._)).Returns(Array.Empty<ForgeCommitInfo>());

    (await _ancestry.GetFirstParentChainAsync(RepositoryId, Repository, Sha(1), 5, Ct)).ShouldBeEmpty();
    A.CallTo(() => _commits.AddMissingAsync(A<long>._, A<IReadOnlyCollection<ForgeCommitInfo>>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .MustNotHaveHappened();
  }

  [Fact]
  public async Task GetChain_DepthZero_ShouldReadNothing()
  {
    (await _ancestry.GetFirstParentChainAsync(RepositoryId, Repository, Sha(1), 0, Ct)).ShouldBeEmpty();
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetChain_NegativeDepth_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _ancestry.GetFirstParentChainAsync(RepositoryId, Repository, Sha(1), -1, Ct));
  }

  private static GitSha Sha(int number) => GitSha.Parse(number.ToString("x40", CultureInfo.InvariantCulture));

  private static ForgeCommitInfo Commit(int number, IReadOnlyList<GitSha> parents) => new(Sha(number), parents, "message", null, Now);

  // The most recent first, as the forge lists them; the numbers start at `first`, each commit the child of the previous number.
  private static List<ForgeCommitInfo> History(int first, int count) =>
    [.. Enumerable.Range(first, count).Reverse().Select(n => Commit(n, n == first ? [] : [Sha(n - 1)]))];

  private void Cache(IEnumerable<ForgeCommitInfo> commits)
  {
    foreach (var commit in commits)
    {
      _cache[commit.Sha] = GitCommit.Create(RepositoryId, commit, Now);
    }
  }

  #endregion
}
