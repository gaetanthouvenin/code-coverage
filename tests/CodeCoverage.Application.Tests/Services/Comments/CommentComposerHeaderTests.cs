// ------------------------------------------------------------------------------------------------
// <copyright file="CommentComposerHeaderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;

using static CodeCoverage.Application.Tests.Services.Comments.CommentSamples;

namespace CodeCoverage.Application.Tests.Services.Comments;

// The header and the diff table of the comment (ARCHITECTURE §8.4), frozen by snapshots. The layout of these tests
// lists the header and the diff only: the other sections have their own tests.
public sealed class CommentComposerHeaderTests
{
  #region Fields

  private static readonly CoverageConfig HeaderAndDiff = Layout("header, diff");

  #endregion

  #region Methods

  [Fact]
  public Task Compose_PatchMissing_ShouldMatchSnapshot() =>
    Verify(CommentComposer.Compose(Content(HeaderAndDiff)));

  [Fact]
  public Task Compose_PatchFullyCovered_ShouldSaySo() =>
    Verify(CommentComposer.Compose(Content(HeaderAndDiff, Comparison(patch: new CoverageTotals(40, 0, 0, 0, 0)))));

  // The base has no report: the table still shows the head, with a question mark for what is missing.
  [Fact]
  public Task Compose_BaseMissing_ShouldWarn() =>
    Verify(CommentComposer.Compose(Content(HeaderAndDiff, withoutBase: true)));

  // The report of the fork point is missing: an older ancestor is compared instead, and the comment says which.
  [Fact]
  public Task Compose_ComparedToNotForkPoint_ShouldExplain() =>
    Verify(CommentComposer.Compose(Content(HeaderAndDiff, comparedTo: OlderSha, hops: 2, behindBy: 3)));

  [Fact]
  public Task Compose_HideProjectCoverage_ShouldOmitDiff() =>
    Verify(
      CommentComposer.Compose(
        Content(
          Config(("layout", ConfigNode.FromText("header, diff")), ("hide_project_coverage", ConfigNode.FromBoolean(true))),
          comparedTo: OlderSha,
          hops: 2,
          behindBy: 3
        )
      )
    );

  [Fact]
  public Task Compose_DiffIncompleteAndInvalidConfiguration_ShouldWarn() =>
    Verify(CommentComposer.Compose(Content(HeaderAndDiff, Comparison(diffIncomplete: true), configInvalid: true)));

  // Without a public address of the Web console, nothing is a link.
  [Fact]
  public Task Compose_NoWebAddress_ShouldWriteNoLink() =>
    Verify(CommentComposer.Compose(Content(HeaderAndDiff, links: Links(null))));

  [Fact]
  public void Compose_ShouldStartWithTheMarkerAndUseLineFeedsOnly()
  {
    var body = CommentComposer.Compose(Content(HeaderAndDiff));

    body.ShouldStartWith(CommentComposer.Marker + "\n## ");
    body.ShouldNotContain("\r");
    body.ShouldNotEndWith("\n");
  }

  [Theory]
  [InlineData("diff")]
  [InlineData("diff, header")]
  [InlineData("diff, condensed_header")]
  [InlineData("header, condensed_header, diff")]
  public void Compose_HeaderWhereverTheLayoutListsIt_ShouldComeFirstAndOnce(string layout)
  {
    var body = CommentComposer.Compose(Content(Layout(layout)));

    body.ShouldBe(CommentComposer.Compose(Content(HeaderAndDiff)));
  }

  [Fact]
  public void Compose_DiffListedTwice_ShouldBeWrittenTwice()
  {
    var body = CommentComposer.Compose(Content(Layout("header, diff, diff")));

    body.Split("```diff").Length.ShouldBe(3);
  }

  [Fact]
  public void Compose_HeaderOnly_ShouldFoldNothing()
  {
    var body = CommentComposer.Compose(Content(Layout("header")));

    body.ShouldNotContain("<details>");
    body.ShouldNotContain("</details>");
    body.ShouldEndWith("to head ([`e4f5a6b`](https://coverage.example.com/github/octo/repo/commit/" + HeadSha.Value + ")).");
  }

  [Fact]
  public void Compose_WithoutTheFold_ShouldWriteTheSectionsInTheOpen()
  {
    var body = CommentComposer.Compose(Content(HeaderAndDiff, wrapDetails: false));

    body.ShouldNotContain("details");
    body.ShouldContain("```diff");
  }

  [Fact]
  public void Compose_Title_ShouldComeFromTheSettingAndBeEscaped()
  {
    var body = CommentComposer.Compose(Content(HeaderAndDiff, title: "Team [Coverage]"));

    body.ShouldContain("## [Team \\[Coverage\\]](https://coverage.example.com/github/octo/repo/pull/123) Report");
  }

  [Theory]
  [InlineData(1, "> Report is 1 commit behind head on main.")]
  [InlineData(3, "> Report is 3 commits behind head on main.")]
  public void Compose_BehindTheBaseBranch_ShouldSayByHowMuch(int behindBy, string expected)
  {
    CommentComposer.Compose(Content(HeaderAndDiff, behindBy: behindBy)).ShouldContain(expected);
  }

  [Theory]
  [InlineData(null)]
  [InlineData(0)]
  public void Compose_NotBehind_ShouldNotMentionIt(int? behindBy)
  {
    CommentComposer.Compose(Content(HeaderAndDiff, behindBy: behindBy)).ShouldNotContain("behind head");
  }

  [Fact]
  public void Compose_OneMissingLine_ShouldBeSingular()
  {
    var body = CommentComposer.Compose(Content(HeaderAndDiff, Comparison(patch: new CoverageTotals(3, 0, 1, 0, 0))));

    body.ShouldContain(":x: Patch coverage is `75.00%` with `1 line` in your changes missing coverage. Please review.");
  }

  // coverage.precision and coverage.round apply to every percentage.
  [Fact]
  public void Compose_Precision_ShouldApplyToEveryPercentage()
  {
    var config = CoverageConfig.Create(
      ConfigMerger.Merge(
        CoverageConfigDefaults.Root,
        ConfigNode.FromMap(
          ("coverage", ConfigNode.FromMap(("precision", ConfigNode.FromNumber(1)), ("round", ConfigNode.FromText("up")))),
          ("comment", ConfigNode.FromMap(("layout", ConfigNode.FromText("header, diff"))))
        )
      )
    );

    var body = CommentComposer.Compose(Content(config));

    body.ShouldContain("Patch coverage is `72.5%`");
    body.ShouldContain("Project coverage is 81.3%.");
    body.ShouldContain("+ Coverage   80.9%   81.3%   +0.4%");
  }

  [Fact]
  public void Compose_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => CommentComposer.Compose(null!));
  }

  #endregion
}
