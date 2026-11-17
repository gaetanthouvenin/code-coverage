// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonMapperTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.PullRequests;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Web.Mapping;

namespace CodeCoverage.Web.Tests.Mapping;

public sealed class ComparisonMapperTests
{
  #region Fields

  private static readonly GitSha Head = GitSha.Parse(new string('e', GitSha.Length));

  private static readonly GitSha Base = GitSha.Parse(new string('b', GitSha.Length));

  private static readonly CoverageConfig Config = CoverageConfig.Create(ConfigNode.FromMap());

  #endregion

  #region Methods

  // Whatever the forge, the link to the comment is the one its links build, written with its escaped segments.
  [Theory]
  [InlineData("https://ghe.example.com/octo%20org/my%20app/pull/423#issuecomment-987")]
  [InlineData("https://gitlab.example.com/octo%20org/my%20app/-/merge_requests/423#note_987")]
  public void ToViewModel_AnyForge_ShouldUseTheForgeLink(string address)
  {
    var links = A.Fake<IForgeLinks>();
    A.CallTo(() => links.CommentUrl("octo org", "my app", 423, 987)).Returns(new Uri(address));

    var detail = ComparisonMapper.ToViewModel(Detail(commentId: 987), links, "octo org", "my app", Config, "github/octo%20org/my%20app");

    detail.CommentUrl.ShouldBe(address);
  }

  [Fact]
  public void ToViewModel_ForgeWithoutLinks_ShouldShowNoLink()
  {
    var detail = ComparisonMapper.ToViewModel(Detail(commentId: 987), null, "octo-org", "app", Config, "gitlab/octo-org/app");

    detail.CommentUrl.ShouldBeNull();
  }

  [Fact]
  public void ToViewModel_NoComment_ShouldNotAskTheForgeLinks()
  {
    var links = A.Fake<IForgeLinks>();

    var detail = ComparisonMapper.ToViewModel(Detail(commentId: null), links, "octo-org", "app", Config, "github/octo-org/app");

    detail.CommentUrl.ShouldBeNull();
    A.CallTo(links).MustNotHaveHappened();
  }

  private static PullRequestDetail Detail(long? commentId)
  {
    var pull = PullRequest.Create(5, new ForgePullInfo(423, ForgePullState.Open, "feat: links", "jo-rivera", Head, "feature/links", "main", Base), DateTimeOffset.UnixEpoch);

    if (commentId is { } id)
    {
      typeof(PullRequest).GetProperty(nameof(PullRequest.CommentId))!.SetValue(pull, id);
    }

    return new PullRequestDetail(pull, null);
  }

  #endregion
}
