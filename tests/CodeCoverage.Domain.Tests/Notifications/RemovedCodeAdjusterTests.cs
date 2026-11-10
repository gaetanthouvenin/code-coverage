// ------------------------------------------------------------------------------------------------
// <copyright file="RemovedCodeAdjusterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Notifications;

using static CodeCoverage.Domain.Tests.Notifications.StatusSamples;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class RemovedCodeAdjusterTests
{
  #region Fields

  // Removes the lines 1 to 4 of the old file, and adds nothing.
  private const string FourLinesRemoved = "@@ -1,5 +1,1 @@\n-a\n-b\n-c\n-d\n e";

  // Removes the line 1 and adds the line 1.
  private const string OneLineChanged = "@@ -1,2 +1,2 @@\n-a\n+A\n b";

  private static readonly IndirectChange SomeChange = new("src/far.cs", IndirectChangeKind.Changed, -1, 1, 0, -10m);

  #endregion

  #region Methods

  [Fact]
  public void AdjustBase_RemovedUncoveredLines_ShouldPass()
  {
    // Base: 8 hits on 10 lines (80%). The change removes 4 hit lines: the head is 4 hits on 6 lines (66.67%),
    // a drop. Without the removed lines the base was 4 on 6 as well: nothing got less tested.
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var head = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4], miss: [5, 6]));
    var subject = Subject(head, @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("66.66% (-13.34%) compared to bbbbbbb, passed because coverage increased by 0% when compared to adjusted base (66.66%)");
  }

  [Fact]
  public void AdjustBase_HeadAboveTheAdjustedBase_ShouldWriteTheIncrease()
  {
    // Base 8/10; 4 hit lines removed give an adjusted base of 4/6 = 66.67%; the head is 7/10 = 70%.
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var subject = Subject(View(7, 3), @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("70.00% (-10.00%) compared to bbbbbbb, passed because coverage increased by 3.33% when compared to adjusted base (66.66%)");
  }

  [Fact]
  public void AdjustBase_RealDrop_ShouldStayFailing()
  {
    // Nothing is removed: the adjusted base is the base.
    var subject = Subject(View(70, 30), View(80, 20), diff: Diff(Modified("src/a.cs", "@@ -1,1 +1,2 @@\n a\n+b")));

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("70.00% (-10.00%) compared to bbbbbbb");
  }

  [Fact]
  public void AdjustBase_RemovedMissedLines_ShouldRaiseTheBaseAndStayFailing()
  {
    // Removing missed lines raises the base: 8/10 becomes 8/8 = 100%.
    var @base = ViewOf(Lines("src/a.cs", hit: [3, 4, 5, 6, 7, 8, 9, 10], miss: [1, 2]));
    var subject = Subject(View(79, 21), @base, diff: Diff(Modified("src/a.cs", "@@ -1,3 +1,1 @@\n-a\n-b\n c")));

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), subject, Format).ShouldBeNull();
  }

  // The tolerance of Codecov: less than 0.005 point under the adjusted base still passes.
  [Theory]
  [InlineData(79996, 20004, true)]
  [InlineData(79995, 20005, false)]
  public void AdjustBase_AtTheTolerance_ShouldPassOnlyUnderIt(int hits, int misses, bool passes)
  {
    var subject = Subject(View(hits, misses), View(80, 20));

    (RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), subject, Format) is not null).ShouldBe(passes);
  }

  [Fact]
  public void AdjustBase_Threshold_ShouldLowerTheAdjustedBase()
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var subject = Subject(View(66, 34), @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    // 66% is under the base minus one point (79%) and under the adjusted base (66.67%), but not under 65.67%.
    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project, ("threshold", Number(1))), subject, Format)
                       .ShouldBe(", passed because coverage increased by 0.33% when compared to adjusted base (65.66%)");
    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), subject, Format).ShouldBeNull();
  }

  [Fact]
  public void AdjustBase_EveryBaseLineRemoved_ShouldStayFailing()
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4], miss: []));
    var subject = Subject(View(1, 1), @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), subject, Format).ShouldBeNull();
  }

  // A numeric target does not move with the code that is removed.
  [Fact]
  public void AdjustBase_NumericTarget_ShouldNotApply()
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var subject = Subject(View(4, 2), @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("target", Number(80))), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("66.66% (target 80.00%)");
  }

  [Fact]
  public void AdjustBase_RenamedFile_ShouldReadTheRemovedLinesUnderThePreviousPath()
  {
    var @base = ViewOf(Lines("src/old.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var head = ViewOf(Lines("src/new.cs", hit: [1, 2, 3, 4], miss: [5, 6]));
    var subject = Subject(head, @base, diff: Diff(FileDiff.Create("src/new.cs", "src/old.cs", FileDiffStatus.Renamed, FourLinesRemoved)));

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), subject, Format).ShouldNotBeNull();
  }

  [Fact]
  public void AdjustBase_FileWithoutPatch_ShouldRemoveNothing()
  {
    var subject = Subject(View(70, 30), View(80, 20), diff: Diff(FileDiff.Create("src/a.cs", null, FileDiffStatus.Modified, null)));

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), subject, Format).ShouldBeNull();
  }

  [Fact]
  public void RemovalsOnly_OnlyCoveredLinesRemoved_ShouldPass()
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var head = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4], miss: [5, 6]));
    var subject = Subject(head, @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("removed_code_behavior", Text("removals_only"))), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("66.66% (-13.34%) compared to bbbbbbb, passed because this change only removed code");
  }

  [Fact]
  public void RemovalsOnly_WithIndirectChanges_ShouldFail()
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var head = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4], miss: [5, 6]));
    var subject = Subject(head, @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)), changes: [SomeChange]);

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("removed_code_behavior", Text("removals_only"))), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("66.66% (-13.34%) compared to bbbbbbb");
  }

  [Fact]
  public void RemovalsOnly_WithAnAddedCoverableLine_ShouldFail()
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2], miss: []));
    var head = ViewOf(Lines("src/a.cs", hit: [2], miss: [1]));
    var subject = Subject(head, @base, diff: Diff(Modified("src/a.cs", OneLineChanged)));

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project, ("removed_code_behavior", Text("removals_only"))), subject, Format).ShouldBeNull();
  }

  [Fact]
  public void RemovalsOnly_NothingCoveredRemoved_ShouldFail()
  {
    // The removed lines were not lines the base report covered.
    var @base = ViewOf(Lines("src/a.cs", hit: [9], miss: [10]));
    var subject = Subject(View(1, 2), @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project, ("removed_code_behavior", Text("removals_only"))), subject, Format).ShouldBeNull();
  }

  // Unlike adjust_base, the two other behaviours do not depend on the target.
  [Fact]
  public void RemovalsOnly_NumericTarget_ShouldStillApply()
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var head = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4], miss: [5, 6]));
    var subject = Subject(head, @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));
    var rule = Rule(StatusType.Project, ("target", Number(80)), ("removed_code_behavior", Text("removals_only")));

    var status = StatusEvaluator.EvaluateProject(rule, subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("66.66% (target 80.00%), passed because this change only removed code");
  }

  [Fact]
  public void FullyCoveredPatch_NullPatch_ShouldPass()
  {
    var subject = Subject(View(70, 30), View(80, 20), patch: PatchCoverage.Empty);

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("removed_code_behavior", Text("fully_covered_patch"))), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("70.00% (-10.00%) compared to bbbbbbb, passed because coverage was not affected by patch");
  }

  [Fact]
  public void FullyCoveredPatch_PatchFullyHit_ShouldPass()
  {
    var subject = Subject(View(70, 30), View(80, 20), patch: Patch(5, 0));

    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("removed_code_behavior", Text("fully_covered_patch"))), subject, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("70.00% (-10.00%) compared to bbbbbbb, passed because patch was fully covered by tests, and no indirect coverage changes");
  }

  [Theory]
  [InlineData(4, 1, 0)]
  [InlineData(4, 0, 1)]
  public void FullyCoveredPatch_PatchNotFullyHit_ShouldFail(int hits, int misses, int partials)
  {
    var subject = Subject(View(70, 30), View(80, 20), patch: Patch(hits, misses, partials));

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project, ("removed_code_behavior", Text("fully_covered_patch"))), subject, Format).ShouldBeNull();
  }

  [Fact]
  public void FullyCoveredPatch_WithIndirectChanges_ShouldFail()
  {
    var subject = Subject(View(70, 30), View(80, 20), patch: Patch(5, 0), changes: [SomeChange]);

    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project, ("removed_code_behavior", Text("fully_covered_patch"))), subject, Format).ShouldBeNull();
  }

  [Theory]
  [InlineData("off")]
  [InlineData("false")]
  public void Off_ShouldNeverPass(string value)
  {
    var @base = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4, 5, 6, 7, 8], miss: [9, 10]));
    var head = ViewOf(Lines("src/a.cs", hit: [1, 2, 3, 4], miss: [5, 6]));
    var subject = Subject(head, @base, diff: Diff(Modified("src/a.cs", FourLinesRemoved)));
    var node = value == "false" ? Bool(false) : Text(value);

    StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("removed_code_behavior", node)), subject, Format).ShouldNotBeNull().State.ShouldBe(StatusState.Failure);
  }

  // A status that already passes is left as it is: nothing is added to its description.
  [Fact]
  public void Evaluate_AlreadyPassing_ShouldNotBeAdjusted()
  {
    var subject = Subject(View(80, 20), View(80, 20), diff: Diff(Modified("src/a.cs", FourLinesRemoved)));

    StatusEvaluator.EvaluateProject(Rule(StatusType.Project), subject, Format).ShouldNotBeNull().Description.ShouldBe("80.00% (+0.00%) compared to bbbbbbb");
  }

  [Fact]
  public void Adjust_WithoutHeadOrBase_ShouldReturnNull()
  {
    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), Subject(null, View(1, 1)), Format).ShouldBeNull();
    RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), Subject(View(1, 1), null), Format).ShouldBeNull();
  }

  [Fact]
  public void Adjust_Null_ShouldThrow()
  {
    var subject = Subject(View(1, 1), View(1, 1));

    Should.Throw<ArgumentNullException>(() => RemovedCodeAdjuster.Adjust(null!, subject, Format));
    Should.Throw<ArgumentNullException>(() => RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), null!, Format));
    Should.Throw<ArgumentNullException>(() => RemovedCodeAdjuster.Adjust(Rule(StatusType.Project), subject, null!));
  }

  private static FileDiff Modified(string path, string patch) => FileDiff.Create(path, null, FileDiffStatus.Modified, patch);

  private static FileCoverage Lines(string path, int[] hit, int[] miss) =>
    FileCoverage.Create(path, [.. hit.Select(static line => new LineCoverage(line, 1, 0, 0, false, null)), .. miss.Select(static line => new LineCoverage(line, 0, 0, 0, false, null))]);

  #endregion
}