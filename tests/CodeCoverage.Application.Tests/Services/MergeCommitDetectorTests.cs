// ------------------------------------------------------------------------------------------------
// <copyright file="MergeCommitDetectorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Services;

public sealed class MergeCommitDetectorTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly ForgeRepositoryRef Address = new(Forge.GitHub, "77", "2002");

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha Head = GitSha.Parse(new string('b', GitSha.Length));

  private static readonly GitSha Base = GitSha.Parse(new string('c', GitSha.Length));

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly IClock _clock = A.Fake<IClock>();

  #endregion

  #region Ctors

  public MergeCommitDetectorTests()
  {
    A.CallTo(() => _gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Detect_ShaEqualsMergeCommitSha_ShouldReturnHead()
  {
    Pull(Head, Sha);

    (await Detect()).ShouldBe(Head);
    A.CallTo(() => _gateway.GetCommitAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Detect_SecondParentIsHead_ShouldReturnHead()
  {
    Pull(Head, Base);
    Parents(Base, Head);

    (await Detect()).ShouldBe(Head);
  }

  [Fact]
  public async Task Detect_RegularCommit_ShouldReturnNull()
  {
    Pull(Head, Base);
    Parents(Base);

    (await Detect()).ShouldBeNull();
  }

  [Fact]
  public async Task Detect_MergeWhoseSecondParentIsNotTheHead_ShouldReturnNull()
  {
    Pull(Head, Base);
    Parents(Base, GitSha.Parse(new string('d', GitSha.Length)));

    (await Detect()).ShouldBeNull();
  }

  [Fact]
  public async Task Detect_ShaIsTheHead_ShouldReturnNullWithoutReadingTheCommit()
  {
    Pull(Sha, Base);

    (await Detect()).ShouldBeNull();
    A.CallTo(() => _gitCommits.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Detect_UnknownPull_ShouldReturnNull()
  {
    A.CallTo(() => _gateway.GetPullAsync(Address, 12, A<CancellationToken>._)).Returns((ForgePullInfo?)null);

    (await Detect()).ShouldBeNull();
  }

  private Task<GitSha?> Detect() => new MergeCommitDetector(_gateways, new CommitAncestry(_gitCommits, _gateways, _clock)).DetectAsync(5, Address, Sha, 12, Ct);

  private void Pull(GitSha head, GitSha mergeCommit) =>
    A.CallTo(() => _gateway.GetPullAsync(Address, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, head, "feature", "main", Base, mergeCommit));

  private void Parents(params GitSha[] parents) =>
    A.CallTo(() => _gitCommits.FindAsync(5, Sha, A<CancellationToken>._))
     .Returns(GitCommit.Create(5, new ForgeCommitInfo(Sha, parents, "message", null, Now), Now));

  #endregion
}
