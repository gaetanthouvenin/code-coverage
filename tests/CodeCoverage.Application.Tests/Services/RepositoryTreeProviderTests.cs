// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTreeProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Services;

public sealed class RepositoryTreeProviderTests
{
  #region Fields

  private const long RepositoryId = 5;

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "101", "1");

  private readonly IRepositoryTreeRepository _trees = A.Fake<IRepositoryTreeRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly RepositoryTreeProvider _provider;

  #endregion

  #region Ctors

  public RepositoryTreeProviderTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    A.CallTo(() => _trees.FindAsync(RepositoryId, Sha, A<CancellationToken>._)).Returns((ForgeTree?)null);

    _provider = new RepositoryTreeProvider(_trees, gateways, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_WhenCached_ShouldNotCallTheForge()
  {
    var cached = new ForgeTree(Sha, [new ForgeTreeEntry("a.cs", Sha, false)], false);
    A.CallTo(() => _trees.FindAsync(RepositoryId, Sha, A<CancellationToken>._)).Returns(cached);

    (await _provider.GetAsync(RepositoryId, Repository, Sha, Ct)).ShouldBe(cached);

    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task Get_WhenMissing_ShouldReadRecursivelyAndCacheTheFilesOnly()
  {
    var read = new ForgeTree(Sha, [new ForgeTreeEntry("src", Sha, true), new ForgeTreeEntry("src/a.cs", Sha, false)], true);
    A.CallTo(() => _gateway.GetTreeAsync(Repository, Sha, true, A<CancellationToken>._)).Returns(read);

    var tree = (await _provider.GetAsync(RepositoryId, Repository, Sha, Ct)).ShouldNotBeNull();

    tree.Entries.ShouldHaveSingleItem().Path.ShouldBe("src/a.cs");
    tree.Truncated.ShouldBeTrue();
    A.CallTo(() => _trees.AddAsync(RepositoryId, tree, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Get_ForgeDoesNotHaveIt_ShouldReturnNullAndCacheNothing()
  {
    A.CallTo(() => _gateway.GetTreeAsync(Repository, Sha, true, A<CancellationToken>._)).Returns((ForgeTree?)null);

    (await _provider.GetAsync(RepositoryId, Repository, Sha, Ct)).ShouldBeNull();

    A.CallTo(() => _trees.AddAsync(A<long>._, A<ForgeTree>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  #endregion
}
