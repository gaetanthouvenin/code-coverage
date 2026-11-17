// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubLinksTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubLinksTests
{
  #region Fields

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  #endregion

  #region Methods

  // The link the console wrote by hand before M7.24, for github.com: the rendered link does not change.
  [Fact]
  public void Comment_PublicGitHub_ShouldMatchTheCurrentLink()
  {
    var links = Links("https://api.github.com");

    links.CommentUrl("octo-org", "app", 423, 987).AbsoluteUri.ShouldBe("https://github.com/octo-org/app/pull/423#issuecomment-987");
  }

  [Fact]
  public void Comment_SegmentsToEscape_ShouldEscapeEachSegment()
  {
    var links = Links("https://api.github.com/");

    links.CommentUrl("octo org", "my#app", 7, 1).AbsoluteUri.ShouldBe("https://github.com/octo%20org/my%23app/pull/7#issuecomment-1");
  }

  [Fact]
  public void PullRequest_PublicGitHub_ShouldLinkThePullRequest()
  {
    Links("https://api.github.com").PullRequestUrl("octo-org", "app", 423).AbsoluteUri.ShouldBe("https://github.com/octo-org/app/pull/423");
  }

  [Fact]
  public void Commit_PublicGitHub_ShouldLinkTheCommit()
  {
    Links("https://api.github.com").CommitUrl("octo-org", "app", Sha).AbsoluteUri.ShouldBe($"https://github.com/octo-org/app/commit/{Sha.Value}");
  }

  [Theory]
  [InlineData("https://api.github.com", "https://github.com/")]
  [InlineData("https://ghe.example.com/api/v3", "https://ghe.example.com/")]
  [InlineData("https://ghe.example.com/api/v3/", "https://ghe.example.com/")]
  [InlineData("https://example.com/github/api/v3", "https://example.com/github/")]
  [InlineData("http://localhost:8080", "http://localhost:8080/")]
  public void SiteOf_ApiAddress_ShouldGiveTheSite(string api, string expected)
  {
    GitHubLinks.SiteOf(new Uri(api)).AbsoluteUri.ShouldBe(expected);
  }

  // GitHub:ApiBaseUrl is read at each call: a change in the settings applies without a restart.
  [Fact]
  public void Comment_ApiAddressChanged_ShouldFollowTheSettings()
  {
    var current = new GitHubOptions { ApiBaseUrl = "https://api.github.com" };
    var options = A.Fake<IOptionsMonitor<GitHubOptions>>();
    A.CallTo(() => options.CurrentValue).ReturnsLazily(() => current);
    var links = new GitHubLinks(options);

    current = new GitHubOptions { ApiBaseUrl = "https://ghe.example.com/api/v3" };

    links.CommentUrl("octo-org", "app", 1, 2).AbsoluteUri.ShouldBe("https://ghe.example.com/octo-org/app/pull/1#issuecomment-2");
  }

  [Theory]
  [InlineData("", "app")]
  [InlineData("octo-org", " ")]
  public void Comment_BlankSegment_ShouldThrow(string owner, string name)
  {
    Should.Throw<ArgumentException>(() => Links("https://api.github.com").CommentUrl(owner, name, 1, 2));
  }

  private static GitHubLinks Links(string apiBaseUrl)
  {
    var options = A.Fake<IOptionsMonitor<GitHubOptions>>();
    A.CallTo(() => options.CurrentValue).Returns(new GitHubOptions { ApiBaseUrl = apiBaseUrl });

    return new GitHubLinks(options);
  }

  #endregion
}
