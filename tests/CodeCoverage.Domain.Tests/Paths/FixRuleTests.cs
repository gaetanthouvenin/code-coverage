// ------------------------------------------------------------------------------------------------
// <copyright file="FixRuleTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Paths;

public sealed class FixRuleTests
{
  #region Methods

  [Fact]
  public void Apply_MatchSlowerThanAFormerTimeout_ShouldStillSubstitute()
  {
    var rule = FixRule.Parse([$"{SlowToMatchPath.Pattern}::fixed"]);

    rule.RefusedFixes.ShouldBeEmpty();
    rule.Apply(SlowToMatchPath.Build(), addPrefixes: false).ShouldBe("fixed");
  }

  #endregion
}
