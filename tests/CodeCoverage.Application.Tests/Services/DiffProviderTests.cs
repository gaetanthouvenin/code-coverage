// ------------------------------------------------------------------------------------------------
// <copyright file="DiffProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Services;

public sealed class DiffProviderTests
{
  #region Fields

  private const long RepositoryId = 5;

  private const int Pull = 7;

  private const string Patch = "@@ -1,1 +1,2 @@\n line\n+added";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Base = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha Head = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "101", "1");

  private readonly IDiffCacheRepository _cache = A.Fake<IDiffCacheRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly DiffProvider _provider;

  #endregion

  #region Ctors

  public DiffProviderTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    A.CallTo(() => _cache.FindAsync(A<long>._, A<GitSha>._, A<GitSha>._, A<DiffSource>._, A<CancellationToken>._)).Returns((DiffSet?)null);

    _provider = new DiffProvider(_cache, gateways, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetPullDiff_Over3000_ShouldMarkIncomplete()
  {
    GivenPullFiles(new ForgePullFiles([.. Enumerable.Range(0, 3000).Select(static i => Changed($"f{i}.cs"))], Truncated: true));

    var diff = (await GetPullDiffAsync()).ShouldNotBeNull();

    diff.Truncated.ShouldBeTrue();
    diff.Complete.ShouldBeFalse();
    diff.Files.Count.ShouldBe(3000);
    diff.Source.ShouldBe(DiffSource.PullFiles);
  }

  [Fact]
  public async Task GetPullDiff_NullPatch_ShouldKeepFileMarkedUnavailable()
  {
    GivenPullFiles(new ForgePullFiles([Changed("binary.png", patch: null)], Truncated: false));

    var diff = (await GetPullDiffAsync()).ShouldNotBeNull();

    var file = diff.Files.ShouldHaveSingleItem();
    file.Path.ShouldBe("binary.png");
    file.HasPatch.ShouldBeFalse();
    diff.Truncated.ShouldBeFalse("a file listed without its patch does not make the set truncated");
  }

  [Fact]
  public async Task GetPullDiff_ShouldCacheWhatTheForgeReturned()
  {
    GivenPullFiles(new ForgePullFiles([Changed("a.cs")], Truncated: false));

    var diff = await GetPullDiffAsync();

    A.CallTo(() => _cache.AddAsync(RepositoryId, Base, Head, diff!, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task GetPullDiff_Cached_ShouldNotCallGitHub()
  {
    var cached = new DiffSet([], Truncated: false, DiffSource.PullFiles);
    A.CallTo(() => _cache.FindAsync(RepositoryId, Base, Head, DiffSource.PullFiles, A<CancellationToken>._)).Returns(cached);

    (await GetPullDiffAsync()).ShouldBeSameAs(cached);

    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetPullDiff_UnknownToTheForge_ShouldReturnNullAndCacheNothing()
  {
    GivenPullFiles(null);

    (await GetPullDiffAsync()).ShouldBeNull();

    A.CallTo(() => _cache.AddAsync(A<long>._, A<GitSha>._, A<GitSha>._, A<DiffSet>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetCompareDiff_Cached_ShouldNotCallGitHub()
  {
    var cached = new DiffSet([], Truncated: true, DiffSource.Compare);
    A.CallTo(() => _cache.FindAsync(RepositoryId, Base, Head, DiffSource.Compare, A<CancellationToken>._)).Returns(cached);

    (await GetCompareDiffAsync()).ShouldBeSameAs(cached);

    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetCompareDiff_ShouldReadTheForgeAndKeepItsTruncation()
  {
    A.CallTo(() => _gateway.CompareAsync(Repository, Base, Head, A<CancellationToken>._))
     .Returns(new ForgeCompareResult(Base, 1, 0, [Changed("a.cs")], Truncated: true));

    var diff = (await GetCompareDiffAsync()).ShouldNotBeNull();

    diff.Truncated.ShouldBeTrue();
    diff.Source.ShouldBe(DiffSource.Compare);
    diff.Files.ShouldHaveSingleItem().AddedLines.ShouldBe([2]);
    A.CallTo(() => _cache.AddAsync(RepositoryId, Base, Head, diff, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task GetCompareDiff_UnknownToTheForge_ShouldReturnNull()
  {
    A.CallTo(() => _gateway.CompareAsync(Repository, Base, Head, A<CancellationToken>._)).Returns((ForgeCompareResult?)null);

    (await GetCompareDiffAsync()).ShouldBeNull();
  }

  [Theory]
  [InlineData(ForgeChangedFileStatus.Added, FileDiffStatus.Added)]
  [InlineData(ForgeChangedFileStatus.Modified, FileDiffStatus.Modified)]
  [InlineData(ForgeChangedFileStatus.Removed, FileDiffStatus.Removed)]
  [InlineData(ForgeChangedFileStatus.Renamed, FileDiffStatus.Renamed)]
  [InlineData(ForgeChangedFileStatus.Copied, FileDiffStatus.Copied)]
  [InlineData(ForgeChangedFileStatus.Unchanged, FileDiffStatus.Unchanged)]
  public async Task GetPullDiff_ShouldTranslateTheStatus(ForgeChangedFileStatus forge, FileDiffStatus expected)
  {
    GivenPullFiles(new ForgePullFiles([new ForgeChangedFile("a.cs", "old.cs", forge, Patch)], Truncated: false));

    var file = (await GetPullDiffAsync()).ShouldNotBeNull().Files.ShouldHaveSingleItem();

    file.Status.ShouldBe(expected);
    file.PreviousPath.ShouldBe("old.cs");
  }

  private static ForgeChangedFile Changed(string path, string? patch = Patch) => new(path, null, ForgeChangedFileStatus.Modified, patch);

  private void GivenPullFiles(ForgePullFiles? files) =>
    A.CallTo(() => _gateway.ListPullFilesAsync(Repository, Pull, A<CancellationToken>._)).Returns(files);

  private Task<DiffSet?> GetPullDiffAsync() => _provider.GetPullDiffAsync(RepositoryId, Repository, Pull, Base, Head, Ct);

  private Task<DiffSet?> GetCompareDiffAsync() => _provider.GetCompareDiffAsync(RepositoryId, Repository, Base, Head, Ct);

  #endregion
}
