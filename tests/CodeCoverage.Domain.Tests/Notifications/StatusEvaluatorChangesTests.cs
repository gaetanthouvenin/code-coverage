// ------------------------------------------------------------------------------------------------
// <copyright file="StatusEvaluatorChangesTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Notifications;

using static CodeCoverage.Domain.Tests.Notifications.StatusSamples;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class StatusEvaluatorChangesTests
{
  #region Fields

  private static readonly IndirectChange MoreMisses = new("src/far.cs", IndirectChangeKind.Changed, -2, 2, 0, -20m);

  private static readonly IndirectChange MorePartials = new("src/other.cs", IndirectChangeKind.Changed, -1, 0, 1, -5m);

  private static readonly IndirectChange MoreHits = new("src/better.cs", IndirectChangeKind.Changed, 2, -2, 0, 20m);

  private static readonly IndirectChange Appeared = new("src/new.cs", IndirectChangeKind.Appeared, 0, 5, 0, null);

  private static readonly IndirectChange Disappeared = new("src/gone.cs", IndirectChangeKind.Disappeared, -5, 0, 0, null);

  #endregion

  #region Methods

  [Fact]
  public void Evaluate_MissesIncreaseOutsideDiff_ShouldFail()
  {
    var status = StatusEvaluator.EvaluateChanges(Changes(), Subject(View(1, 1), View(1, 1), changes: [MoreMisses]), Format);

    status.State.ShouldBe(StatusState.Failure);
    status.Context.ShouldBe("coverage/changes");
    status.Description.ShouldBe("1 file has indirect coverage changes not visible in diff");
  }

  [Fact]
  public void Evaluate_SeveralFailingFiles_ShouldCountThem()
  {
    var status = StatusEvaluator.EvaluateChanges(Changes(), Subject(View(1, 1), View(1, 1), changes: [MoreMisses, MorePartials, MoreHits]), Format);

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("2 files have indirect coverage changes not visible in diff");
  }

  [Fact]
  public void Evaluate_NoChange_ShouldSucceed()
  {
    var status = StatusEvaluator.EvaluateChanges(Changes(), Subject(View(1, 1), View(1, 1)), Format);

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No indirect coverage changes found");
  }

  // Better coverage elsewhere, a file that appears or disappears: listed in the comparison, never failing.
  [Fact]
  public void Evaluate_OnlyChangesThatAreNotWorthNoting_ShouldSucceed()
  {
    var status = StatusEvaluator.EvaluateChanges(Changes(), Subject(View(1, 1), View(1, 1), changes: [MoreHits, Appeared, Disappeared]), Format);

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No indirect coverage changes found");
  }

  [Theory]
  [InlineData("auto")]
  [InlineData("pr")]
  public void Evaluate_PullWithoutBaseReport_ShouldSayItCannotTell(string @base)
  {
    var status = StatusEvaluator.EvaluateChanges(Changes(("base", Text(@base))), Subject(View(1, 1), null), Format);

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("Unable to determine changes, no report found at pull request base");
  }

  [Fact]
  public void Evaluate_PushBuildWithoutBaseReport_ShouldFindNoChange()
  {
    StatusEvaluator.EvaluateChanges(Changes(), Subject(View(1, 1), null, pull: false), Format).Description.ShouldBe("No indirect coverage changes found");
  }

  [Fact]
  public void Evaluate_Informational_ShouldSendSuccessKeepDescription()
  {
    var rule = Changes(("informational", Bool(true)));

    var status = StatusEvaluator.Evaluate(rule, Subject(View(1, 1), View(1, 1), changes: [MoreMisses]), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("1 file has indirect coverage changes not visible in diff");
  }

  [Theory]
  [InlineData(StatusType.Project, "70.00% (-10.00%) compared to bbbbbbb")]
  [InlineData(StatusType.Patch, "50.00% of diff hit (target 80.00%)")]
  public void Evaluate_InformationalProjectOrPatch_ShouldSendSuccessKeepDescription(StatusType type, string description)
  {
    var subject = Subject(View(70, 30), View(80, 20), Patch(1, 1));
    var blocking = Rule(type, ("removed_code_behavior", Text("off")));
    var informational = Rule(type, ("removed_code_behavior", Text("off")), ("informational", Bool(true)));

    var failing = StatusEvaluator.Evaluate(blocking, subject, Format).ShouldNotBeNull();
    var informed = StatusEvaluator.Evaluate(informational, subject, Format).ShouldNotBeNull();

    failing.State.ShouldBe(StatusState.Failure);
    failing.Description.ShouldBe(description);
    informed.State.ShouldBe(StatusState.Success);
    informed.Description.ShouldBe(description);
    informed.Context.ShouldBe(failing.Context);
  }

  [Fact]
  public void Evaluate_InformationalWithAnErrorOfIfNotFound_ShouldSendSuccess()
  {
    var rule = Rule(StatusType.Project, ("if_not_found", Text("error")), ("informational", Bool(true)));

    var status = StatusEvaluator.Evaluate(rule, Subject(null, null), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No coverage information found on head");
  }

  [Fact]
  public void Evaluate_InformationalIgnored_ShouldStillPostNothing()
  {
    var rule = Rule(StatusType.Project, ("if_not_found", Text("ignore")), ("informational", Bool(true)));

    StatusEvaluator.Evaluate(rule, Subject(null, null), Format).ShouldBeNull();
  }

  [Fact]
  public void Evaluate_ShouldDispatchOnTheTypeOfTheRule()
  {
    var subject = Subject(View(80, 20), View(80, 20), Patch(8, 2));

    StatusEvaluator.Evaluate(Rule(StatusType.Project), subject, Format).ShouldNotBeNull().Context.ShouldBe("coverage/project");
    StatusEvaluator.Evaluate(Rule(StatusType.Patch), subject, Format).ShouldNotBeNull().Context.ShouldBe("coverage/patch");
    StatusEvaluator.Evaluate(Changes(), subject, Format).ShouldNotBeNull().Context.ShouldBe("coverage/changes");
  }

  // The longest description a rule can give is cut at the limit of the forge.
  [Fact]
  public void Evaluate_LongDescription_ShouldBeCutAt140Characters()
  {
    var format = new StatusFormat("coverage", 28, RoundingMode.Down);
    var rule = Rule(StatusType.Project, ("removed_code_behavior", Text("fully_covered_patch")));

    var status = StatusEvaluator.Evaluate(rule, Subject(View(2, 1), View(200, 99), Patch(5, 0)), format).ShouldNotBeNull();

    status.Description.Length.ShouldBe(CommitStatus.DescriptionMaxLength);
    status.Description.ShouldStartWith("66.6666700000000000000000000000% (");
  }

  [Fact]
  public void Evaluate_NotAChangesRuleOrNull_ShouldThrow()
  {
    var subject = Subject(View(1, 1), null);

    Should.Throw<ArgumentException>(() => StatusEvaluator.EvaluateChanges(Rule(StatusType.Project), subject, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluateChanges(null!, subject, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluateChanges(Changes(), null!, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluateChanges(Changes(), subject, null!));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.Evaluate(null!, subject, Format));
  }

  private static StatusRule Changes(params (string Key, ConfigNode Value)[] entries) => Rule(StatusType.Changes, entries);

  #endregion
}