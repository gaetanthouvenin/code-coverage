// ------------------------------------------------------------------------------------------------
// <copyright file="StatusEvaluatorPatchTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Notifications;

using static CodeCoverage.Domain.Tests.Notifications.StatusSamples;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class StatusEvaluatorPatchTests
{
  #region Methods

  [Fact]
  public void Evaluate_NoCoverableLines_ShouldSucceedNotAffected()
  {
    var status = StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch), Subject(View(1, 9), View(9, 1), PatchCoverage.Empty), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Context.ShouldBe("coverage/patch");
    // From the fork point, where the patch starts.
    status.Description.ShouldBe("Coverage not affected when comparing ccccccc...aaaaaaa");
  }

  [Fact]
  public void Evaluate_NoCoverableLinesOnAPushBuild_ShouldNameTheBase()
  {
    var status = StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch), Subject(View(1, 9), View(9, 1), pull: false), Format).ShouldNotBeNull();

    status.Description.ShouldBe("Coverage not affected when comparing bbbbbbb...aaaaaaa");
  }

  // Nothing to compare with and nothing to cover: still a success, even with if_not_found: failure.
  [Fact]
  public void Evaluate_NoCoverableLinesWithoutAnyBase_ShouldSucceedWithoutNamingACommit()
  {
    var rule = Rule(StatusType.Patch, ("if_not_found", Text("failure")));

    var status = StatusEvaluator.EvaluatePatch(rule, Subject(View(1, 9), null, pull: false), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("Coverage not affected");
  }

  [Fact]
  public void Evaluate_WithinThreshold_ShouldUseThresholdText()
  {
    // 78% of the patch against a base of 80%, with five points of threshold.
    var rule = Rule(StatusType.Patch, ("threshold", Text("5%")));

    var status = StatusEvaluator.EvaluatePatch(rule, Subject(View(1, 1), View(80, 20), Patch(78, 22)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("78.00% of diff hit (within 5.00% threshold of 80.00%)");
  }

  [Theory]
  [InlineData(80, 20, StatusState.Success, "80.00% of diff hit (target 80.00%)")]
  [InlineData(95, 5, StatusState.Success, "95.00% of diff hit (target 80.00%)")]
  [InlineData(79, 21, StatusState.Failure, "79.00% of diff hit (target 80.00%)")]
  [InlineData(0, 4, StatusState.Failure, "0.00% of diff hit (target 80.00%)")]
  public void Evaluate_AutoTarget_ShouldBeTheCoverageOfTheBase(int hits, int misses, StatusState expected, string description)
  {
    var status = StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch), Subject(View(1, 1), View(80, 20), Patch(hits, misses)), Format).ShouldNotBeNull();

    status.State.ShouldBe(expected);
    status.Description.ShouldBe(description);
  }

  // Exactly at the threshold, and one line under it.
  [Theory]
  [InlineData(750, 250, StatusState.Success, "75.00% of diff hit (within 5.00% threshold of 80.00%)")]
  [InlineData(749, 251, StatusState.Failure, "74.90% of diff hit (target 80.00%)")]
  public void Evaluate_AtTheExactThreshold_ShouldSucceedOnlyWhenReached(int hits, int misses, StatusState expected, string description)
  {
    var rule = Rule(StatusType.Patch, ("threshold", Number(5)));

    var status = StatusEvaluator.EvaluatePatch(rule, Subject(View(1, 1), View(80, 20), Patch(hits, misses)), Format).ShouldNotBeNull();

    status.State.ShouldBe(expected);
    status.Description.ShouldBe(description);
  }

  [Theory]
  [InlineData(90, 10, StatusState.Success, "90.00% of diff hit (target 90.00%)")]
  [InlineData(89, 11, StatusState.Failure, "89.00% of diff hit (target 90.00%)")]
  public void Evaluate_NumericTarget_ShouldNeedNoBase(int hits, int misses, StatusState expected, string description)
  {
    var rule = Rule(StatusType.Patch, ("target", Text("90%")));

    var status = StatusEvaluator.EvaluatePatch(rule, Subject(View(1, 1), null, Patch(hits, misses)), Format).ShouldNotBeNull();

    status.State.ShouldBe(expected);
    status.Description.ShouldBe(description);
  }

  [Theory]
  [InlineData("success", StatusState.Success)]
  [InlineData("failure", StatusState.Failure)]
  [InlineData("error", StatusState.Error)]
  public void Evaluate_AutoWithoutBase_ShouldUseIfNotFound(string behavior, StatusState expected)
  {
    var rule = Rule(StatusType.Patch, ("if_not_found", Text(behavior)));

    var status = StatusEvaluator.EvaluatePatch(rule, Subject(View(1, 1), null, Patch(1, 1)), Format).ShouldNotBeNull();

    status.State.ShouldBe(expected);
    status.Description.ShouldBe("No report found to compare against");
  }

  [Fact]
  public void Evaluate_AutoWithABaseWithoutCoverage_ShouldUseIfNotFound()
  {
    StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch), Subject(View(1, 1), ViewOf(), Patch(1, 1)), Format).ShouldNotBeNull().Description.ShouldBe("No report found to compare against");
  }

  [Fact]
  public void Evaluate_IfNotFoundIgnore_ShouldPostNothing()
  {
    StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch, ("if_not_found", Text("ignore"))), Subject(View(1, 1), null, Patch(1, 1)), Format).ShouldBeNull();
  }

  // A partial line of the patch is a line, not a hit: 1 hit and 1 partial give 50%.
  [Fact]
  public void Evaluate_PartialLines_ShouldCountInTheLinesOnly()
  {
    var rule = Rule(StatusType.Patch, ("target", Number(50)));

    StatusEvaluator.EvaluatePatch(rule, Subject(View(1, 1), null, Patch(1, 0, 1)), Format).ShouldNotBeNull().Description.ShouldBe("50.00% of diff hit (target 50.00%)");
  }

  // 2/3 of the patch against a base of 2/3: equal from their integers, whatever their rounding.
  [Fact]
  public void Evaluate_ShouldCompareWithDecimalFromIntegers()
  {
    var status = StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch), Subject(View(1, 1), View(200, 100), Patch(2, 1)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("66.66% of diff hit (target 66.66%)");
  }

  [Fact]
  public void Evaluate_NamedRule_ShouldNameItsContext()
  {
    StatusEvaluator.EvaluatePatch(Named(StatusType.Patch, "api"), Subject(View(1, 1), View(1, 1)), Format with { Prefix = "codecov" }).ShouldNotBeNull().Context.ShouldBe("codecov/patch/api");
  }

  [Fact]
  public void Evaluate_NotAPatchRuleOrNull_ShouldThrow()
  {
    var subject = Subject(View(1, 1), null);

    Should.Throw<ArgumentException>(() => StatusEvaluator.EvaluatePatch(Rule(StatusType.Project), subject, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluatePatch(null!, subject, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch), null!, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluatePatch(Rule(StatusType.Patch), subject, null!));
  }

  #endregion
}