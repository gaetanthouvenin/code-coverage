// ------------------------------------------------------------------------------------------------
// <copyright file="StatusEvaluatorProjectTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Notifications;

using static CodeCoverage.Domain.Tests.Notifications.StatusSamples;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class StatusEvaluatorProjectTests
{
  #region Methods

  [Fact]
  public void Evaluate_AutoWithinThreshold_ShouldSucceed()
  {
    // 79% against a base of 80%, with one point of threshold.
    var rule = Rule(StatusType.Project, ("threshold", Text("1%")));

    var status = StatusEvaluator.EvaluateProject(rule, Subject(View(79, 21), View(80, 20)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Context.ShouldBe("coverage/project");
    status.Description.ShouldBe("79.00% (-1.00%) compared to bbbbbbb");
  }

  [Fact]
  public void Evaluate_AutoBelowTheBase_ShouldFail()
  {
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(View(79, 21), View(80, 20)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("79.00% (-1.00%) compared to bbbbbbb");
  }

  [Fact]
  public void Evaluate_AutoEqualToTheBase_ShouldSucceedWithAPlusZero()
  {
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(View(80, 20), View(8, 2)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("80.00% (+0.00%) compared to bbbbbbb");
  }

  [Fact]
  public void Evaluate_AutoAboveTheBase_ShouldSucceedWithItsSign()
  {
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(View(85, 15), View(80, 20)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("85.00% (+5.00%) compared to bbbbbbb");
  }

  // Exactly at the threshold: the base minus the threshold is still reached.
  [Theory]
  [InlineData(790, 210, StatusState.Success)]
  [InlineData(789, 211, StatusState.Failure)]
  public void Evaluate_AutoAtTheExactThreshold_ShouldSucceedOnlyWhenReached(int hits, int misses, StatusState expected)
  {
    var rule = Rule(StatusType.Project, ("threshold", Number(1)));

    StatusEvaluator.EvaluateProject(rule, Subject(View(hits, misses), View(800, 200)), Format).ShouldNotBeNull().State.ShouldBe(expected);
  }

  [Fact]
  public void Evaluate_ShouldCompareWithDecimalFromIntegers()
  {
    // 2/3 against a target written with five decimals. Rounded as it is stored, the head reads 66.66667 and
    // would reach the target; computed from its integers it is 66.666666..., which does not.
    var rule = Rule(StatusType.Project, ("target", Number(66.66667m)));

    var status = StatusEvaluator.EvaluateProject(rule, Subject(View(2, 1), View(1, 1)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("66.66% (target 66.66%)");
  }

  // Two reports with the same ratio and different sizes are equal, whatever their rounding.
  [Fact]
  public void Evaluate_AutoSameRatioFromOtherIntegers_ShouldSucceed()
  {
    StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(View(2, 1), View(200, 100)), Format).ShouldNotBeNull().State.ShouldBe(StatusState.Success);
  }

  [Theory]
  [InlineData(80, 20, StatusState.Success, "80.00% (target 80.00%)")]
  [InlineData(79, 21, StatusState.Failure, "79.00% (target 80.00%)")]
  [InlineData(100, 0, StatusState.Success, "100.00% (target 80.00%)")]
  public void Evaluate_NumericTarget_ShouldCompareWithIt(int hits, int misses, StatusState expected, string description)
  {
    var rule = Rule(StatusType.Project, ("target", Text("80%")));

    // A numeric target ignores the coverage of the base: only its existence counts.
    var status = StatusEvaluator.EvaluateProject(rule, Subject(View(hits, misses), View(50, 50)), Format).ShouldNotBeNull();

    status.State.ShouldBe(expected);
    status.Description.ShouldBe(description);
    StatusEvaluator.EvaluateProject(rule, Subject(View(hits, misses), View(1, 99)), Format).ShouldNotBeNull().Description.ShouldBe(description);
  }

  [Fact]
  public void Evaluate_NumericTargetWithinThreshold_ShouldSucceed()
  {
    var rule = Rule(StatusType.Project, ("target", Number(80)), ("threshold", Number(2)));

    var status = StatusEvaluator.EvaluateProject(rule, Subject(View(78, 22), View(1, 1)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("78.00% (target 80.00%)");
  }

  [Fact]
  public void Evaluate_NoHead_ShouldUseIfNotFound()
  {
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(null, View(80, 20)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No coverage information found on head");
  }

  [Theory]
  [InlineData("success", StatusState.Success)]
  [InlineData("failure", StatusState.Failure)]
  [InlineData("error", StatusState.Error)]
  public void Evaluate_NothingToCompare_ShouldTakeTheStateOfIfNotFound(string behavior, StatusState expected)
  {
    var rule = Rule(StatusType.Project, ("if_not_found", Text(behavior)));

    StatusEvaluator.EvaluateProject(rule, Subject(null, null), Format).ShouldNotBeNull().State.ShouldBe(expected);
    StatusEvaluator.EvaluateProject(rule, Subject(View(1, 1), null), Format).ShouldNotBeNull().State.ShouldBe(expected);
  }

  // No line the head report can cover: there is no coverage to speak of, even with a numeric target.
  [Fact]
  public void Evaluate_HeadWithoutAnyCoverableLine_ShouldUseIfNotFound()
  {
    var rule = Rule(StatusType.Project, ("target", Number(80)), ("if_not_found", Text("failure")));

    var status = StatusEvaluator.EvaluateProject(rule, Subject(ViewOf(), View(80, 20)), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("No coverage information found on head");
  }

  [Fact]
  public void Evaluate_AutoWithoutBase_ShouldUseIfNotFound()
  {
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(View(80, 20), null), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No report found to compare against");
  }

  [Fact]
  public void Evaluate_AutoWithABaseWithoutAnyCoverableLine_ShouldUseIfNotFound()
  {
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(View(80, 20), ViewOf()), Format).ShouldNotBeNull();

    status.Description.ShouldBe("No coverage information found on base report");
  }

  [Fact]
  public void Evaluate_IfNotFoundIgnore_ShouldPostNothing()
  {
    var rule = Rule(StatusType.Project, ("if_not_found", Text("ignore")));

    StatusEvaluator.EvaluateProject(rule, Subject(null, null), Format).ShouldBeNull();
    // Only what cannot be compared is ignored.
    StatusEvaluator.EvaluateProject(rule, Subject(View(1, 1), View(1, 1)), Format).ShouldNotBeNull();
  }

  // Codecov looks for the base before it reads the target: without a base report, a numeric target is not compared.
  [Theory]
  [InlineData(79, "success", StatusState.Success)]
  [InlineData(85, "failure", StatusState.Failure)]
  [InlineData(85, "error", StatusState.Error)]
  public void Evaluate_NumericTargetWithoutBase_ShouldUseIfNotFound(int hits, string behavior, StatusState expected)
  {
    var rule = Rule(StatusType.Project, ("target", Text("80%")), ("if_not_found", Text(behavior)));

    var status = StatusEvaluator.EvaluateProject(rule, Subject(View(hits, 100 - hits), null), Format).ShouldNotBeNull();

    status.State.ShouldBe(expected);
    status.Description.ShouldBe("No report found to compare against");
  }

  [Fact]
  public void Evaluate_NumericTargetWithABaseWithoutAnyCoverableLine_ShouldUseIfNotFound()
  {
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("target", Number(80))), Subject(View(79, 21), ViewOf()), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No coverage information found on base report");
  }

  [Fact]
  public void Evaluate_NamedRule_ShouldNameItsContext()
  {
    var rule = Named(StatusType.Project, "backend");

    StatusEvaluator.EvaluateProject(rule, Subject(View(1, 1), View(1, 1)), Format with { Prefix = "codecov" }).ShouldNotBeNull().Context.ShouldBe("codecov/project/backend");
  }

  // The numbers of Codecov: rounded as coverage.round says, with every decimal of coverage.precision.
  [Theory]
  [InlineData(RoundingMode.Down, 2, "66.66% (-0.01%) compared to bbbbbbb")]
  [InlineData(RoundingMode.Up, 2, "66.67% (-0.00%) compared to bbbbbbb")]
  [InlineData(RoundingMode.Nearest, 2, "66.67% (-0.00%) compared to bbbbbbb")]
  [InlineData(RoundingMode.Down, 0, "66% (-1%) compared to bbbbbbb")]
  [InlineData(RoundingMode.Nearest, 4, "66.6667% (-0.0033%) compared to bbbbbbb")]
  public void Evaluate_ShouldWriteItsNumbersAsCodecovRoundsThem(RoundingMode rounding, int precision, string description)
  {
    // 66.66667 against 66.67000: a drop of 0.00333.
    var status = StatusEvaluator.EvaluateProject(Rule(StatusType.Project), Subject(View(2, 1), View(6667, 3333)), new StatusFormat("coverage", precision, rounding)).ShouldNotBeNull();

    status.Description.ShouldBe(description);
    status.State.ShouldBe(StatusState.Failure);
  }

  [Fact]
  public void Evaluate_PartialLines_ShouldCountInTheLinesOnly()
  {
    // 1 hit, 1 partial: 50%, not 75%.
    StatusEvaluator.EvaluateProject(Rule(StatusType.Project, ("target", Number(50))), Subject(View(1, 0, 1), View(1, 1)), Format).ShouldNotBeNull().Description.ShouldBe("50.00% (target 50.00%)");
  }

  [Fact]
  public void Evaluate_NotAProjectRuleOrNull_ShouldThrow()
  {
    var subject = Subject(View(1, 1), null);

    Should.Throw<ArgumentException>(() => StatusEvaluator.EvaluateProject(Rule(StatusType.Patch), subject, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluateProject(null!, subject, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluateProject(Rule(StatusType.Project), null!, Format));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.EvaluateProject(Rule(StatusType.Project), subject, null!));
  }

  #endregion
}