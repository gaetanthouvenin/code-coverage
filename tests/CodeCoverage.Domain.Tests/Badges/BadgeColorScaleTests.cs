// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeColorScaleTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Badges;
using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Tests.Badges;

public sealed class BadgeColorScaleTests
{
  #region Fields

  private const string Red = "#e05d44";

  private const string BrightGreen = "#44cc11";

  #endregion

  #region Methods

  [Theory]
  [InlineData(70)]
  [InlineData(0)]
  [InlineData(69.99)]
  public void Color_AtLow_ShouldBeRed(decimal coverage)
  {
    BadgeColorScale.Color(coverage, null).ShouldBe(Red);
  }

  // Without coverage.range in the raw YAML, the badge uses [70, 100], as the badges of Codecov, never the [60, 80] of
  // the configuration defaults.
  [Fact]
  public void Color_RawYamlRangeAbsent_ShouldUse70To100()
  {
    BadgeColorScale.Color(85m, null).ShouldBe(BadgeColorScale.Color(85m, Range(70, 100)));
    BadgeColorScale.Color(99.99m, null).ShouldNotBe(BrightGreen);
    BadgeColorScale.Color(100m, null).ShouldBe(BrightGreen);
    BadgeColorScale.Color(65m, null).ShouldBe(Red);
  }

  [Fact]
  public void Color_ExplicitRange_ShouldScaleOverIt()
  {
    var range = Range(60, 80);

    BadgeColorScale.Color(80m, range).ShouldBe(BrightGreen);
    BadgeColorScale.Color(60m, range).ShouldBe(Red);
    BadgeColorScale.Color(70m, range).ShouldBe(BadgeColorScale.Color(85m, null));
  }

  [Fact]
  public void Color_ShouldGoThroughTheStopsOfTheScale()
  {
    // Step 20 of 100 on [0, 100] is the second stop, step 60 the fourth.
    var range = Range(0, 100);

    BadgeColorScale.Color(20m, range).ShouldBe("#fe7d37");
    BadgeColorScale.Color(60m, range).ShouldBe("#a4a61d");
  }

  [Fact]
  public void Of_WithoutCoverage_ShouldBeUnknown()
  {
    var badge = BadgeColorScale.Of(null, null);

    badge.ShouldBeSameAs(Badge.Unknown);
    badge.IsUnknown.ShouldBeTrue();
    badge.Color.ShouldBe(BadgeColorScale.UnknownColor);
  }

  private static CoverageRange Range(decimal low, decimal high) =>
    CoverageRange.Read(ConfigNode.FromList(ConfigNode.FromNumber(low), ConfigNode.FromNumber(high))) ?? throw new InvalidOperationException("Bad range.");

  #endregion
}
