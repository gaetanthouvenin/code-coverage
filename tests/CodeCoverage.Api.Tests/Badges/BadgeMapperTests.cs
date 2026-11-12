// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeMapperTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Api.Mapping;
using CodeCoverage.Domain.Badges;

namespace CodeCoverage.Api.Tests.Badges;

// The shown value follows Codecov's "%.{p}f" % coverage; every expectation is the output of Python for that value.
public sealed class BadgeMapperTests
{
  #region Methods

  [Theory]
  [InlineData("85.678", 0, "86")]
  [InlineData("85.678", 1, "85.7")]
  [InlineData("85.678", 2, "85.68")]
  [InlineData("85.5", 0, "86")]
  [InlineData("84.5", 0, "84")]
  [InlineData("85.49", 0, "85")]
  [InlineData("85.75", 1, "85.8")]
  [InlineData("85.25", 1, "85.2")]
  [InlineData("85.24", 1, "85.2")]
  [InlineData("85.375", 2, "85.38")]
  [InlineData("85.125", 2, "85.12")]
  [InlineData("85.124", 2, "85.12")]
  [InlineData("85.675", 2, "85.67")]
  [InlineData("99.5", 0, "100")]
  [InlineData("100", 2, "100.00")]
  [InlineData("0", 0, "0")]
  public void Value_ShouldRoundToTheNearestAsCodecov(string coverage, int precision, string expected)
  {
    var badge = new Badge(decimal.Parse(coverage, CultureInfo.InvariantCulture), "#4c1");

    BadgeMapper.Value(badge, precision).ShouldBe(expected);
  }

  [Fact]
  public void Value_Unknown_ShouldBeNull() => BadgeMapper.Value(Badge.Unknown, 2).ShouldBeNull();

  // The colour reads the coverage before rounding: 99.5 shows 100 but is not the colour of a full coverage.
  [Fact]
  public void Value_RoundedUpToFull_ShouldKeepTheColourOfTheRawCoverage()
  {
    var badge = BadgeColorScale.Of(99.5m, null);

    BadgeMapper.Value(badge, 0).ShouldBe("100");
    badge.Color.ShouldNotBe(BadgeColorScale.Of(100m, null).Color);
  }

  #endregion
}
