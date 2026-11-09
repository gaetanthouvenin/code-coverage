// ------------------------------------------------------------------------------------------------
// <copyright file="UnifiedDiffParserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Tests.Diffs;

public sealed class UnifiedDiffParserTests
{
  #region Methods

  [Fact]
  public void Parse_MultipleHunks_ShouldTrackBothCounters()
  {
    var hunks = UnifiedDiffParser.Parse(
      Patch(
        "@@ -1,4 +1,5 @@ class Sample",
        " a",
        "-b",
        "+c",
        "+d",
        " e",
        " f",
        "@@ -10,3 +11,2 @@ void Run()",
        " g",
        "-h",
        " i"));

    hunks.Count.ShouldBe(2);
    ShouldBe(hunks[0], new Hunk(1, 4, 1, 5, [2], [2, 3]));
    ShouldBe(hunks[1], new Hunk(10, 3, 11, 2, [11], []));
  }

  [Fact]
  public void Parse_NoNewlineMarker_ShouldIgnore()
  {
    var hunks = UnifiedDiffParser.Parse(
      Patch("@@ -1 +1 @@", "-before", @"\ No newline at end of file", "+after", @"\ No newline at end of file"));

    hunks.ShouldHaveSingleItem().RemovedLines.ShouldBe([1]);
    hunks[0].AddedLines.ShouldBe([1]);
  }

  [Fact]
  public void Parse_HeaderWithoutCounts_ShouldCountOneLine()
  {
    var hunk = UnifiedDiffParser.Parse(Patch("@@ -7 +9 @@", "-a", "+b")).ShouldHaveSingleItem();

    ShouldBe(hunk, new Hunk(7, 1, 9, 1, [7], [9]));
  }

  [Fact]
  public void Parse_NewFile_ShouldNumberAddedLinesFromOne()
  {
    var hunk = UnifiedDiffParser.Parse(Patch("@@ -0,0 +1,3 @@", "+a", "+b", "+c")).ShouldHaveSingleItem();

    hunk.RemovedLines.ShouldBeEmpty();
    hunk.AddedLines.ShouldBe([1, 2, 3]);
  }

  [Fact]
  public void Parse_DeletedFile_ShouldOnlyNumberRemovedLines()
  {
    var hunk = UnifiedDiffParser.Parse(Patch("@@ -1,2 +0,0 @@", "-a", "-b")).ShouldHaveSingleItem();

    hunk.RemovedLines.ShouldBe([1, 2]);
    hunk.AddedLines.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_ContentLikeFileHeader_ShouldCountTheLine()
  {
    // Codecov skips these two lines: see the remarks of UnifiedDiffParser.Parse.
    var hunk = UnifiedDiffParser.Parse(Patch("@@ -1,3 +1,3 @@", "--- a comment", "+++ b", " c", " d"))
      .ShouldHaveSingleItem();

    hunk.RemovedLines.ShouldBe([1]);
    hunk.AddedLines.ShouldBe([1]);
  }

  [Fact]
  public void Parse_CarriageReturnInsideLine_ShouldNotStartAnotherLine()
  {
    var hunk = UnifiedDiffParser.Parse("@@ -1,2 +1,2 @@\n a\rb\n-c\n+d").ShouldHaveSingleItem();

    hunk.RemovedLines.ShouldBe([2]);
    hunk.AddedLines.ShouldBe([2]);
  }

  [Fact]
  public void Parse_UnicodeLineSeparator_ShouldNotStartAnotherLine()
  {
    var hunk = UnifiedDiffParser.Parse($"@@ -1,2 +1,2 @@\n a{(char)0x2028}b\n-c\n+d").ShouldHaveSingleItem();

    hunk.RemovedLines.ShouldBe([2]);
  }

  [Fact]
  public void Parse_CrLfPatch_ShouldKeepTheNumbers()
  {
    var hunk = UnifiedDiffParser.Parse("@@ -1,2 +1,2 @@\r\n a\r\n-b\r\n+c\r\n").ShouldHaveSingleItem();

    hunk.RemovedLines.ShouldBe([2]);
    hunk.AddedLines.ShouldBe([2]);
  }

  [Fact]
  public void Parse_FileHeaders_ShouldBeSkipped()
  {
    var hunk = UnifiedDiffParser.Parse(
        Patch("diff --git a/x b/x", "index 1..2 100644", "--- a/x", "+++ b/x", "@@ -1 +1 @@", "-a", "+b", string.Empty))
      .ShouldHaveSingleItem();

    hunk.RemovedLines.ShouldBe([1]);
  }

  [Fact]
  public void Parse_EmptyPatch_ShouldReturnNoHunk()
  {
    UnifiedDiffParser.Parse(string.Empty).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("@@ nope @@")]
  [InlineData("@@ -1,a +1 @@")]
  [InlineData("@@@ -1 -1 +1 @@@")]
  [InlineData("@@ -99999999999 +1 @@")]
  public void Parse_InvalidHeader_ShouldThrow(string header)
  {
    Should.Throw<FormatException>(() => UnifiedDiffParser.Parse(Patch(header, "-a", "+b")));
  }

  [Theory]
  [InlineData("@@ -1,2 +1,2 @@\n a")]
  [InlineData("@@ -1,2 +1,2 @@\n a\n\n b")]
  [InlineData("@@ -1,1 +1,1 @@\n-a\n-b\n+c")]
  [InlineData("@@ -1,1 +1,2 @@\n a\n b\n+c")]
  [InlineData("@@ -1,2 +1,2 @@\n a\n@@ -5 +5 @@\n-b\n+c")]
  public void Parse_BodyNotMatchingTheCounts_ShouldThrow(string patch)
  {
    Should.Throw<FormatException>(() => UnifiedDiffParser.Parse(patch));
  }

  [Fact]
  public void Parse_NullPatch_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => UnifiedDiffParser.Parse(null!));
  }

  private static void ShouldBe(Hunk actual, Hunk expected)
  {
    (actual.OldStart, actual.OldCount, actual.NewStart, actual.NewCount)
      .ShouldBe((expected.OldStart, expected.OldCount, expected.NewStart, expected.NewCount));
    actual.RemovedLines.ShouldBe(expected.RemovedLines);
    actual.AddedLines.ShouldBe(expected.AddedLines);
  }

  private static string Patch(params string[] lines)
  {
    return string.Join('\n', lines);
  }

  #endregion
}
