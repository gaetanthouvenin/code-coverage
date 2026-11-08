// ------------------------------------------------------------------------------------------------
// <copyright file="RequireChangesExpressionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

// The expected results follow CoverageCommentRequirementSchemaField (libs/shared/shared/validation/helpers.py)
// and HasEnoughRequiredChanges (apps/worker/services/notification/notifiers/comment/conditions.py) of Codecov.
public sealed class RequireChangesExpressionTests
{
  #region Fields

  private static readonly RequireChangesFacts AnyChangeOnly = Facts(indirect: true, head: 80m, origin: 80m);

  private static readonly RequireChangesFacts CoverageDropOnly = Facts(head: 70m, origin: 80m);

  #endregion

  #region Methods

  [Theory]
  // Codecov parses this text as [0b010, 0b101]: "or" binds inside "and". Read from left to right,
  // "(coverage_drop and any_change) or uncovered_patch" would hold in the first line.
  [InlineData("coverage_drop and any_change or uncovered_patch", false, false, true, false)]
  [InlineData("coverage_drop and any_change or uncovered_patch", true, false, true, true)]
  [InlineData("coverage_drop and any_change or uncovered_patch", true, true, false, true)]
  [InlineData("coverage_drop and any_change or uncovered_patch", true, false, false, false)]
  [InlineData("coverage_drop and any_change or uncovered_patch", false, true, false, false)]
  // [0b011, 0b100]
  [InlineData("any_change or coverage_drop and uncovered_patch", false, false, false, false)]
  [InlineData("any_change or coverage_drop and uncovered_patch", true, false, false, false)]
  [InlineData("any_change or coverage_drop and uncovered_patch", true, false, true, true)]
  public void Evaluate_AndOr_ShouldGroupOrInsideAnd(string text, bool drop, bool indirect, bool uncovered, bool expected)
  {
    var facts = Facts(indirect: indirect, lines: uncovered ? 2 : 0, patch: uncovered ? 50m : null, head: drop ? 70m : 80m);

    Read(text).Evaluate(facts).ShouldBe(expected);
  }

  [Theory]
  [InlineData(null, 80)]
  [InlineData(80, null)]
  [InlineData(null, null)]
  public void Evaluate_CoverageDropMissingData_ShouldBeTrue(int? head, int? origin)
  {
    Read("coverage_drop").Evaluate(Facts(head: head, origin: origin)).ShouldBeTrue();
  }

  [Theory]
  [InlineData("79.99000", false)]
  [InlineData("79.98999", true)]
  [InlineData("80.00000", false)]
  [InlineData("81.00000", false)]
  public void Evaluate_CoverageDrop_ShouldNeedMoreThanAHundredth(string head, bool expected)
  {
    var facts = Facts(head: decimal.Parse(head, CultureInfo.InvariantCulture), origin: 80m);

    Read("coverage_drop").Evaluate(facts).ShouldBe(expected);
  }

  [Theory]
  [InlineData(1.0, false)]
  [InlineData(0.5, true)]
  public void Evaluate_CoverageDrop_ShouldTakeTheThresholdIntoAccount(double threshold, bool expected)
  {
    var facts = Facts(head: 79.2m, origin: 80m, threshold: (decimal)threshold);

    Read("coverage_drop").Evaluate(facts).ShouldBe(expected);
  }

  [Theory]
  [InlineData(true, 0, true)]
  [InlineData(false, 3, true)]
  [InlineData(false, 0, false)]
  public void Evaluate_AnyChange_ShouldNeedIndirectChangesOrPatchLines(bool indirect, int lines, bool expected)
  {
    Read("any_change").Evaluate(Facts(indirect: indirect, lines: lines)).ShouldBe(expected);
  }

  [Theory]
  [InlineData(0, null, false)]
  [InlineData(3, "100.00000", false)]
  [InlineData(3, "99.99000", false)]
  [InlineData(3, "99.98999", true)]
  [InlineData(3, "50.00000", true)]
  public void Evaluate_UncoveredPatch_ShouldNeedAnIncompletePatch(int lines, string? patch, bool expected)
  {
    var coverage = patch is null ? (decimal?)null : decimal.Parse(patch, CultureInfo.InvariantCulture);

    Read("uncovered_patch").Evaluate(Facts(lines: lines, patch: coverage)).ShouldBe(expected);
  }

  [Fact]
  public void Evaluate_None_ShouldAlwaysHold()
  {
    RequireChangesExpression.None.Evaluate(Facts()).ShouldBeTrue();
  }

  [Fact]
  public void Evaluate_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => RequireChangesExpression.None.Evaluate(null!));
  }

  [Fact]
  public void Read_False_ShouldAlwaysHold()
  {
    RequireChangesExpression.Read(Bool(false))!.Evaluate(Facts()).ShouldBeTrue();
  }

  [Fact]
  public void Read_True_ShouldMeanAnyChange()
  {
    var expression = RequireChangesExpression.Read(Bool(true))!;

    expression.Evaluate(Facts()).ShouldBeFalse();
    expression.Evaluate(AnyChangeOnly).ShouldBeTrue();
  }

  [Theory]
  [InlineData("COVERAGE_DROP")]
  [InlineData("coverage_drop OR any_change")]
  [InlineData("  coverage_drop   or   any_change  ")]
  public void Read_ShouldIgnoreCaseAndSpaces(string text)
  {
    Read(text).Evaluate(CoverageDropOnly).ShouldBeTrue();
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("sometimes")]
  [InlineData("any_change or")]
  [InlineData("and any_change")]
  [InlineData("any_change coverage_drop")]
  [InlineData("any_change xor coverage_drop")]
  [InlineData("coverage_drop+any_change")]
  [InlineData("coverage_drop + any_change")]
  public void Read_MalformedText_ShouldBeNull(string text)
  {
    RequireChangesExpression.Read(Text(text)).ShouldBeNull();
  }

  [Fact]
  public void Read_OtherKinds_ShouldBeNull()
  {
    RequireChangesExpression.Read(Number(42)).ShouldBeNull();
    RequireChangesExpression.Read(Texts("coverage_drop")).ShouldBeNull();
    RequireChangesExpression.Read(ConfigNode.Null).ShouldBeNull();
  }

  private static RequireChangesExpression Read(string text) => RequireChangesExpression.Read(Text(text))!;

  private static RequireChangesFacts Facts(
    bool indirect = false,
    int lines = 0,
    decimal? patch = null,
    decimal? head = 80m,
    decimal? origin = 80m,
    decimal threshold = 0m) =>
    new(indirect, lines, patch, head, origin, threshold);

  #endregion
}
