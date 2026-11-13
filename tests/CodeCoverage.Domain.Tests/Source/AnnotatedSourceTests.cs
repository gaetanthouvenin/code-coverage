// ------------------------------------------------------------------------------------------------
// <copyright file="AnnotatedSourceTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Source;

namespace CodeCoverage.Domain.Tests.Source;

public sealed class AnnotatedSourceTests
{
  #region Fields

  private static readonly GitSha Sha = GitSha.Parse(new string('c', GitSha.Length));

  private static readonly FileCoverage File = FileCoverage.Create(
    "src/A.cs",
    [new LineCoverage(1, 3, 0, 0, false, null), new LineCoverage(2, 0, 0, 0, false, null), new LineCoverage(4, 2, 1, 2, false, ["1"]), new LineCoverage(9, 1, 0, 0, false, null)]);

  #endregion

  #region Methods

  [Fact]
  public void Merge_ShouldGiveEachLineItsCoverage()
  {
    var source = AnnotatedSource.Merge(File, Sha, null, "a\nb\nc\nd\n");

    source.IsSourceAvailable.ShouldBeTrue();
    source.Lines.Select(static l => l.Text).ShouldBe(["a", "b", "c", "d"]);
    source.Lines.Select(static l => l.State).ShouldBe([LineState.Hit, LineState.Miss, null, LineState.Partial]);
    source.Lines[0].Hits.ShouldBe(3);
    source.Lines[2].Hits.ShouldBeNull();
    (source.Lines[3].Covered, source.Lines[3].Total).ShouldBe((1, 2));
    source.Totals.ShouldBe(File.Totals);
  }

  // A line of the report beyond the end of the text (line 9 here) is ignored: the text decides the lines.
  [Fact]
  public void Merge_ReportLineBeyondTheEnd_ShouldBeIgnored()
  {
    var source = AnnotatedSource.Merge(File, Sha, null, "a\nb");

    source.Lines.Count.ShouldBe(2);
  }

  [Fact]
  public void Merge_CrlfAndByteOrderMark_ShouldLeaveNeitherInTheText()
  {
    var source = AnnotatedSource.Merge(File, Sha, "unit", "\uFEFFa\r\nb\r\n");

    source.Lines.Select(static l => l.Text).ShouldBe(["a", "b"]);
    source.Flag.ShouldBe("unit");
  }

  [Fact]
  public void Merge_NoFinalEndOfLine_ShouldKeepTheLastLine()
  {
    AnnotatedSource.Merge(File, Sha, null, "a\n\nc").Lines.Select(static l => l.Text).ShouldBe(["a", "", "c"]);
  }

  [Fact]
  public void Merge_EmptyText_ShouldHaveNoLine()
  {
    var source = AnnotatedSource.Merge(File, Sha, null, string.Empty);

    source.IsSourceAvailable.ShouldBeTrue();
    source.Lines.ShouldBeEmpty();
  }

  [Fact]
  public void WithPatch_ShouldMarkTheAddedLinesAndNameThePull()
  {
    var source = AnnotatedSource.Merge(File, Sha, null, "a\nb\nc\n").WithPatch(12, [2, 9]);

    source.PatchPull.ShouldBe(12);
    source.Lines.Select(static l => l.InPatch).ShouldBe([false, true, false]);
  }

  // Without a cached diff the pull request is named, nothing is marked.
  [Fact]
  public void WithPatch_NoAddedLine_ShouldMarkNothing()
  {
    var source = AnnotatedSource.Merge(File, Sha, null, "a\nb\n").WithPatch(12, null);

    source.PatchPull.ShouldBe(12);
    source.Lines.ShouldAllBe(static l => !l.InPatch);
  }

  // Unavailable keeps the totals of the file: the page still shows them.
  [Theory]
  [InlineData(null)]
  [InlineData("PNG\0\u0001")]
  public void Merge_NoTextOrBinary_ShouldBeUnavailableWithTotals(string? text)
  {
    var source = AnnotatedSource.Merge(File, Sha, null, text);

    source.IsSourceAvailable.ShouldBeFalse();
    source.Lines.ShouldBeEmpty();
    source.Totals.ShouldBe(File.Totals);
    source.Path.ShouldBe("src/A.cs");
  }

  #endregion
}
