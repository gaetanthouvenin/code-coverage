// ------------------------------------------------------------------------------------------------
// <copyright file="WebLinksTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Services;

// The routes of ARCHITECTURE §10bis.3.
public sealed class WebLinksTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  #endregion

  #region Methods

  [Fact]
  public void Links_ShouldFollowTheRoutesOfTheConsole()
  {
    var links = Links("https://coverage.example.com/", "octo/repo");

    links.Pull(123).ShouldBe("https://coverage.example.com/github/octo/repo/pull/123");
    links.Commit(Sha).ShouldBe($"https://coverage.example.com/github/octo/repo/commit/{Sha.Value}");
    links.File(Sha, "src/Api/Foo.cs", 123).ShouldBe($"https://coverage.example.com/github/octo/repo/commit/{Sha.Value}/blob/src/Api/Foo.cs?pull=123");
    links.File(Sha, "src/Api/Foo.cs", null).ShouldBe($"https://coverage.example.com/github/octo/repo/commit/{Sha.Value}/blob/src/Api/Foo.cs");
  }

  [Fact]
  public void File_ShouldEscapeEachSegmentOfThePath()
  {
    var link = Links("https://coverage.example.com", "octo/repo").File(Sha, "docs/a b/c#d?.md", null);

    link.ShouldBe($"https://coverage.example.com/github/octo/repo/commit/{Sha.Value}/blob/docs/a%20b/c%23d%3F.md");
  }

  [Fact]
  public void Links_ForgeWithLevelsBetweenTheAccountAndTheRepository_ShouldJoinThemWithColons()
  {
    var repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("group/sub/team/repo"), "main", false, "badge", Now);

    new WebLinks("https://coverage.example.com", Forge.GitLab, repository).Pull(1)
                                                                          .ShouldBe("https://coverage.example.com/gitlab/group:sub:team/repo/pull/1");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void Links_WithoutAPublicAddress_ShouldLeadNowhere(string? web)
  {
    var links = Links(web, "octo/repo");

    links.Pull(123).ShouldBeNull();
    links.Commit(Sha).ShouldBeNull();
    links.File(Sha, "a.cs", 1).ShouldBeNull();
  }

  [Fact]
  public void Links_NullArguments_ShouldThrow()
  {
    var links = Links("https://coverage.example.com", "octo/repo");

    Should.Throw<ArgumentNullException>(() => new WebLinks("https://coverage.example.com", Forge.GitHub, null!));
    Should.Throw<ArgumentNullException>(() => links.Commit(null!));
    Should.Throw<ArgumentNullException>(() => links.File(null!, "a.cs", null));
    Should.Throw<ArgumentNullException>(() => links.File(Sha, null!, null));
  }

  private static WebLinks Links(string? web, string slug) =>
    new(web, Forge.GitHub, TrackedRepository.Create(42, "7", RepositorySlug.Parse(slug), "main", false, "badge", Now));

  #endregion
}
