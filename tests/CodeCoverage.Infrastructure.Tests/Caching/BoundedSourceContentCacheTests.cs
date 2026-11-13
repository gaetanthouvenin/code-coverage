// ------------------------------------------------------------------------------------------------
// <copyright file="BoundedSourceContentCacheTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Caching;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure.Tests.Caching;

public sealed class BoundedSourceContentCacheTests : IDisposable
{
  #region Fields

  private static readonly ForgeRepositoryRef Repository = new(Forge.GitHub, "42", "1296269");

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly SourceContentMemory _memory = new();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  #endregion

  #region Ctors

  public BoundedSourceContentCacheTests()
  {
    A.CallTo(() => _forges.GetGateway(Forge.GitHub)).Returns(_gateway);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The second view, a prerender's second pass among them, is served without reading the forge again.
  [Fact]
  public async Task GetSource_SameFileTwice_ShouldReadOnceWithinTheBound()
  {
    Returns(new ForgeFileContent("class A {}"));
    var cache = CacheOf(1);

    (await cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBe("class A {}");
    (await cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBe("class A {}");

    A.CallTo(() => _gateway.GetBoundedContentAsync(Repository, "src/A.cs", Sha, BoundedSourceContentCache.MaxBytes, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // ADR-013: the key starts with the organization of the scope; the same repository reference read by another
  // organization is read again, never served from the first one's entry.
  [Fact]
  public async Task GetSource_TwoOrganizations_ShouldNotShare()
  {
    A.CallTo(() => _gateway.GetBoundedContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<int>._, A<CancellationToken>._))
     .ReturnsNextFromSequence(new ForgeFileContent("first"), new ForgeFileContent("second"));

    (await CacheOf(1).GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBe("first");
    (await CacheOf(2).GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBe("second");
    (await CacheOf(1).GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBe("first");
  }

  // A rate limit is « unavailable » but never kept: the next view reads again.
  [Fact]
  public async Task GetSource_RateLimited_ShouldNotKeep()
  {
    A.CallTo(() => _gateway.GetBoundedContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<int>._, A<CancellationToken>._))
     .Throws(new GitHubRateLimitedException(DateTimeOffset.UnixEpoch)).Once()
     .Then.Returns(new ForgeFileContent("back"));
    var cache = CacheOf(1);

    (await cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBeNull();
    (await cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBe("back");
  }

  // Absent, too large and binary are verdicts of the file itself: kept, so they cost one reading.
  [Theory]
  [InlineData("absent")]
  [InlineData("too-large")]
  [InlineData("binary")]
  public async Task GetSource_UnavailableVerdict_ShouldBeKept(string verdict)
  {
    Returns(verdict switch
    {
      "absent" => null,
      "too-large" => ForgeFileContent.TooLarge,
      var _ => new ForgeFileContent("PNG\0\u0001"),
    });
    var cache = CacheOf(1);

    (await cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBeNull();
    (await cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBeNull();

    A.CallTo(() => _gateway.GetBoundedContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // ADR-014: a forge prepared without its adapter has nothing to read.
  [Fact]
  public async Task GetSource_ForgeWithoutAdapter_ShouldBeUnavailable()
  {
    var repository = new ForgeRepositoryRef(Forge.GitLab, "1", "2");
    A.CallTo(() => _forges.GetGateway(Forge.GitLab)).Throws(new NotSupportedException("GitLab"));

    (await CacheOf(1).GetSourceAsync(repository, Sha, "src/A.cs", Ct)).ShouldBeNull();
  }

  // Any other refusal of the forge is what it is: the page shows its toast.
  [Fact]
  public async Task GetSource_BadGateway_ShouldBeRaisedAndNotKept()
  {
    A.CallTo(() => _gateway.GetBoundedContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<int>._, A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "down")).Once()
     .Then.Returns(new ForgeFileContent("back"));
    var cache = CacheOf(1);

    await Should.ThrowAsync<BadGatewayException>(() => cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct));
    (await cache.GetSourceAsync(Repository, Sha, "src/A.cs", Ct)).ShouldBe("back");
  }

  // Each entry costs its characters against the bound of the dedicated memory, never the shared cache.
  [Fact]
  public async Task GetSource_ShouldChargeItsSizeToTheBoundedMemory()
  {
    Returns(new ForgeFileContent(new string('x', 1000)));

    await CacheOf(1).GetSourceAsync(Repository, Sha, "src/A.cs", Ct);

    _memory.Cache.GetCurrentStatistics()!.CurrentEstimatedSize.ShouldBe(1000);
  }

  private void Returns(ForgeFileContent? content) =>
    A.CallTo(() => _gateway.GetBoundedContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).Returns(content);

  private BoundedSourceContentCache CacheOf(long organizationId)
  {
    var organization = new OrganizationContext();
    organization.Enter(organizationId);

    return new BoundedSourceContentCache(_memory, organization, _forges, NullLogger<BoundedSourceContentCache>.Instance);
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _memory.Dispose();

  #endregion
}
