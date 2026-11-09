// ------------------------------------------------------------------------------------------------
// <copyright file="LineShifterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Tests.Diffs;

public sealed class LineShifterTests
{
  #region Methods

  [Fact]
  public void Shift_AfterInsertions_ShouldOffsetFollowingLines()
  {
    var diff = Diff("@@ -1,2 +1,4 @@", " a", "+b", "+c", " d");

    Shifted(diff, 1, 2, 3).ShouldBe([(1, 1), (2, 4), (3, 5)]);
  }

  [Fact]
  public void Shift_DeletedLine_ShouldDropIt()
  {
    var diff = Diff("@@ -1,3 +1,2 @@", " a", "-b", " c");

    Shifted(diff, 1, 2, 3).ShouldBe([(1, 1), (3, 2)]);
  }

  [Fact]
  public void Shift_ModifiedLine_ShouldDropIt()
  {
    var diff = Diff("@@ -1,3 +1,3 @@", " a", "-b", "+B", " c");

    Shifted(diff, 1, 2, 3).ShouldBe([(1, 1), (3, 3)]);
  }

  [Fact]
  public void Shift_LineBeforeFirstHunk_ShouldKeepItsNumber()
  {
    var diff = Diff("@@ -10,2 +10,3 @@", " a", "+b", " c");

    Shifted(diff, 3, 9).ShouldBe([(3, 3), (9, 9)]);
  }

  [Fact]
  public void Shift_LineAfterLastHunk_ShouldUseTheTotalOffset()
  {
    var diff = Diff("@@ -2,3 +2,2 @@", " a", "-b", " c", "@@ -8,2 +7,5 @@", " d", "+e", "+f", "+g", " h");

    Shifted(diff, 100).ShouldBe([(100, 102)]);
  }

  // The expected numbers are those of test_shift_lines_by_diff_mixed_changes in Codecov.
  [Fact]
  public void Shift_MixedChanges_ShouldMatchCodecov()
  {
    var diff = Diff(
      "@@ -3,3 +3,4 @@",
      " some go code in line 3",
      "-this line was removed",
      "+this line was added",
      "+this line was also added",
      " ",
      "@@ -9,1 +10,5 @@",
      " some go code in line 9",
      "+add",
      "+add",
      "+add",
      "+add");

    Shifted(diff, 1, 2, 3, 5, 6, 8, 9, 10)
      .ShouldBe([(1, 1), (2, 2), (3, 3), (5, 6), (6, 7), (8, 9), (9, 10), (10, 15)]);
  }

  // The expected numbers are those of test_shift_lines_by_diff_only_adds in Codecov.
  [Fact]
  public void Shift_OnlyAdditions_ShouldMatchCodecov()
  {
    var diff = Diff(
      "@@ -3,3 +3,4 @@",
      " some go code in line 3",
      "+this line was added",
      " ",
      " ",
      "@@ -8,3 +9,6 @@",
      " some go code in line 8",
      "+add",
      " ",
      "+add",
      "+add",
      " ");

    Shifted(diff, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)
      .ShouldBe([(1, 1), (2, 2), (3, 3), (4, 5), (5, 6), (6, 7), (7, 8), (8, 9), (9, 11), (10, 14)]);
  }

  [Fact]
  public void Shift_RemovedCommentLine_ShouldNotShiftItsNeighbours()
  {
    // Written "--- x", as a removed "-- x" SQL or Lua comment: Codecov leaves it in place.
    var diff = Diff("@@ -1,3 +1,2 @@", " a", "--- x", " b");

    Shifted(diff, 1, 2, 3).ShouldBe([(1, 1), (3, 2)]);
  }

  [Fact]
  public void Shift_ZeroContextDeletion_ShouldFollowTheOldNumbers()
  {
    // GitHub always sends context lines; `git diff -U0` does not. Codecov removes lines 4 and 5 here.
    var diff = Diff("@@ -5,2 +4,0 @@", "-a", "-b");

    Shifted(diff, 4, 5, 6, 7).ShouldBe([(4, 4), (7, 5)]);
  }

  [Fact]
  public void Shift_LinesInAnyOrder_ShouldReturnThemAscending()
  {
    var diff = Diff("@@ -1,2 +1,3 @@", " a", "+b", " c");

    Shifted(diff, 2, 1).ShouldBe([(1, 1), (2, 3)]);
  }

  [Fact]
  public void Shift_ShouldKeepTheCoverageOfTheLine()
  {
    var diff = Diff("@@ -1,1 +1,2 @@", "+a", " b");
    var line = new LineCoverage(1, 7, 1, 2, true, ["b1"]);

    LineShifter.Shift(diff, [line]).ShouldHaveSingleItem().ShouldBe(line with { Line = 2 });
  }

  [Fact]
  public void Shift_NoLines_ShouldReturnNone()
  {
    LineShifter.Shift(Diff("@@ -1 +1 @@", "-a", "+b"), []).ShouldBeEmpty();
  }

  [Fact]
  public void Shift_DiffWithoutPatch_ShouldThrow()
  {
    var diff = FileDiff.Create("a.cs", null, FileDiffStatus.Modified, null);

    Should.Throw<ArgumentException>(() => LineShifter.Shift(diff, [])).ParamName.ShouldBe("diff");
  }

  [Fact]
  public void Shift_NullArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => LineShifter.Shift(null!, []));
    Should.Throw<ArgumentNullException>(() => LineShifter.Shift(Diff("@@ -1 +1 @@", "-a", "+b"), null!));
  }

  private static FileDiff Diff(params string[] lines)
  {
    return FileDiff.Create("a.cs", null, FileDiffStatus.Modified, string.Join('\n', lines));
  }

  // Each line carries its old number in Hits, to compare (old number, new number).
  private static (int Old, int New)[] Shifted(FileDiff diff, params int[] lines)
  {
    return
    [
      .. LineShifter.Shift(diff, lines.Select(line => new LineCoverage(line, line, 0, 0, false, null)))
        .Select(line => ((int)line.Hits, line.Line)),
    ];
  }

  #endregion
}
