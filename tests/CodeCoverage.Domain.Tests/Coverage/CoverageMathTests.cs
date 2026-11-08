// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageMathTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Coverage;

// The expected values come from Codecov's own expressions, run with Python 3.12: ratio() of
// shared/helpers/numeric.py, and precise_round() with format_number_to_str() of the worker.
public sealed class CoverageMathTests
{
  #region Methods

  [Fact]
  public void Ratio_WithPartials_ShouldCountThemInDenominatorOnly()
  {
    var totals = new CoverageTotals(hits: 1, misses: 1, partials: 2, branches: 0, methods: 0);

    totals.Lines.ShouldBe(4);
    totals.Coverage.ShouldBe(25m);
  }

  // With 2 and 6 of 64000 lines, Math.Round(double, 5, ToEven) gives another result than Python.
  // With 23 and 49 of 1280 lines, multiplying by 100 before dividing gives another result.
  [Theory]
  [InlineData(1, 3, "33.33333")]
  [InlineData(2, 3, "66.66667")]
  [InlineData(1, 2, "50.00000")]
  [InlineData(7, 8, "87.50000")]
  [InlineData(1, 7, "14.28571")]
  [InlineData(6, 7, "85.71429")]
  [InlineData(7002, 10000, "70.02000")]
  [InlineData(99999, 100000, "99.99900")]
  [InlineData(1, 2000000, "0.00005")]
  [InlineData(2, 64000, "0.00313")]
  [InlineData(6, 64000, "0.00937")]
  [InlineData(10, 64000, "0.01562")]
  [InlineData(1, 6400, "0.01562")]
  [InlineData(23, 1280, "1.79687")]
  [InlineData(49, 1280, "3.82813")]
  public void Ratio_ShouldMatchPythonRoundAt5Decimals(int hits, int lines, string expected)
  {
    CoverageMath.Ratio(hits, lines).ShouldBe(Parse(expected));
  }

  [Fact]
  public void Ratio_WithoutLines_ShouldBeNull()
  {
    CoverageMath.Ratio(0, 0).ShouldBeNull();
  }

  [Fact]
  public void Ratio_AllHit_ShouldBe100()
  {
    CoverageMath.Ratio(3, 3).ShouldBe(100m);
  }

  [Fact]
  public void Ratio_NoHit_ShouldBeZero()
  {
    CoverageMath.Ratio(0, 3).ShouldBe(0m);
  }

  [Theory]
  [InlineData(-1, 3, "hits")]
  [InlineData(1, -3, "lines")]
  [InlineData(4, 3, "hits")]
  public void Ratio_WithInvalidCounters_ShouldThrowArgumentOutOfRange(
    int hits,
    int lines,
    string parameter)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => CoverageMath.Ratio(hits, lines))
          .ParamName.ShouldBe(parameter);
  }

  [Fact]
  public void Ratio_WithCommaCulture_ShouldKeepItsValue()
  {
    var previous = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      CoverageMath.Ratio(1, 3).ShouldBe(33.33333m);
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }
  }

  [Fact]
  public void Classify_BranchLine_ShouldIgnoreHits()
  {
    CoverageMath.Classify(new LineCoverage(1, 42, 0, 2, false, null)).ShouldBe(LineState.Miss);
    CoverageMath.Classify(new LineCoverage(1, 0, 2, 2, false, null)).ShouldBe(LineState.Hit);
    CoverageMath.Classify(new LineCoverage(1, 42, 1, 2, false, null)).ShouldBe(LineState.Partial);
  }

  [Theory]
  [InlineData(1L, LineState.Hit)]
  [InlineData(0L, LineState.Miss)]
  [InlineData(4294967296L, LineState.Hit)]
  public void Classify_PlainLine_ShouldFollowHits(long hits, LineState expected)
  {
    CoverageMath.Classify(new LineCoverage(1, hits, 0, 0, false, null)).ShouldBe(expected);
  }

  [Fact]
  public void Classify_CoveredAboveTotal_ShouldBePartial()
  {
    CoverageMath.Classify(new LineCoverage(1, 1, 3, 2, false, null)).ShouldBe(LineState.Partial);
  }

  [Fact]
  public void Classify_MethodLineWithoutHits_ShouldBeMiss()
  {
    CoverageMath.Classify(new LineCoverage(1, 0, 0, 0, true, null)).ShouldBe(LineState.Miss);
  }

  [Theory]
  [InlineData("70.029", 2, "70.02")]
  [InlineData("70.02", 2, "70.02")]
  [InlineData("100", 2, "100.00")]
  [InlineData("0", 2, "0.00")]
  [InlineData("70.029", 0, "70")]
  [InlineData("70.12345", 5, "70.12345")]
  public void Format_Down_ShouldTruncate(string value, int precision, string expected)
  {
    CoverageMath.Format(Parse(value), precision, RoundingMode.Down).ShouldBe(expected);
  }

  [Fact]
  public void Format_DownOnNegative_ShouldRoundTowardNegativeInfinity()
  {
    CoverageMath.Format(-0.015m, 2, RoundingMode.Down).ShouldBe("-0.02");
  }

  [Theory]
  [InlineData("70.021", "70.03")]
  [InlineData("-0.019", "-0.01")]
  public void Format_Up_ShouldRoundTowardPositiveInfinity(string value, string expected)
  {
    CoverageMath.Format(Parse(value), 2, RoundingMode.Up).ShouldBe(expected);
  }

  [Theory]
  [InlineData("70.125", 2, "70.12")]
  [InlineData("70.135", 2, "70.14")]
  [InlineData("70.5", 0, "70")]
  public void Format_Nearest_ShouldRoundHalfToEven(string value, int precision, string expected)
  {
    CoverageMath.Format(Parse(value), precision, RoundingMode.Nearest).ShouldBe(expected);
  }

  [Theory]
  [InlineData("0.001", 2, RoundingMode.Down, "<0.01")]
  [InlineData("0.001", 2, RoundingMode.Nearest, "<0.01")]
  [InlineData("0.00001", 3, RoundingMode.Down, "<0.001")]
  [InlineData("0.00001", 4, RoundingMode.Down, "<0.0001")]
  [InlineData("0.000001", 5, RoundingMode.Down, "<0.00001")]
  [InlineData("0.4", 0, RoundingMode.Down, "<1")]
  [InlineData("-0.001", 2, RoundingMode.Nearest, "-<0.01")]
  [InlineData("-0.001", 2, RoundingMode.Up, "-<0.01")]
  public void Format_TinyNonZero_ShouldShowLessThan001(
    string value,
    int precision,
    RoundingMode rounding,
    string expected)
  {
    CoverageMath.Format(Parse(value), precision, rounding).ShouldBe(expected);
  }

  [Theory]
  [InlineData("0.001", RoundingMode.Up, "0.01")]
  [InlineData("-0.001", RoundingMode.Down, "-0.01")]
  public void Format_TinyRoundedAwayFromZero_ShouldShowTheRoundedValue(
    string value,
    RoundingMode rounding,
    string expected)
  {
    CoverageMath.Format(Parse(value), 2, rounding).ShouldBe(expected);
  }

  [Fact]
  public void Format_Null_ShouldShowEmptySet()
  {
    CoverageMath.Format(null, 2, RoundingMode.Down).ShouldBe("∅");
  }

  [Fact]
  public void Format_WithCommaCulture_ShouldKeepTheDecimalPoint()
  {
    var previous = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      CoverageMath.Format(70.02m, 2, RoundingMode.Down).ShouldBe("70.02");
      CoverageMath.Format(0.001m, 2, RoundingMode.Down).ShouldBe("<0.01");
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }
  }

  [Fact]
  public void Format_AtMaxPrecision_ShouldKeepAllDecimals()
  {
    var expected = "1." + new string('0', CoverageMath.MaxPrecision);

    CoverageMath.Format(1m, CoverageMath.MaxPrecision, RoundingMode.Down).ShouldBe(expected);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(29)]
  public void Format_WithInvalidPrecision_ShouldThrowArgumentOutOfRange(int precision)
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => CoverageMath.Format(1m, precision, RoundingMode.Down)
    ).ParamName.ShouldBe("precision");

    Should.Throw<ArgumentOutOfRangeException>(
      () => CoverageMath.Format(null, precision, RoundingMode.Down)
    ).ParamName.ShouldBe("precision");
  }

  [Theory]
  [InlineData("1.5", RoundingMode.Down, "+1.50")]
  [InlineData("-1.5", RoundingMode.Down, "-1.50")]
  [InlineData("0.001", RoundingMode.Down, "+<0.01")]
  [InlineData("-0.001", RoundingMode.Nearest, "-<0.01")]
  public void FormatDelta_ShouldShowTheSign(string value, RoundingMode rounding, string expected)
  {
    CoverageMath.FormatDelta(Parse(value), 2, rounding).ShouldBe(expected);
  }

  [Theory]
  [InlineData("1.2345", 3, RoundingMode.Down, "+1.234")]
  [InlineData("1.239", 2, RoundingMode.Down, "+1.23")]
  [InlineData("1.239", 2, RoundingMode.Nearest, "+1.24")]
  [InlineData("1.231", 2, RoundingMode.Up, "+1.24")]
  public void FormatDelta_ShouldFollowPrecisionAndRounding(
    string value,
    int precision,
    RoundingMode rounding,
    string expected)
  {
    CoverageMath.FormatDelta(Parse(value), precision, rounding).ShouldBe(expected);
  }

  [Theory]
  [InlineData("0")]
  [InlineData("0.00000")]
  public void FormatDelta_Zero_ShouldShowNoImpact(string value)
  {
    CoverageMath.FormatDelta(Parse(value), 2, RoundingMode.Down).ShouldBe("ø");
  }

  [Fact]
  public void FormatDelta_Null_ShouldShowEmptySet()
  {
    CoverageMath.FormatDelta(null, 2, RoundingMode.Down).ShouldBe("∅");
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(29)]
  public void FormatDelta_WithInvalidPrecision_ShouldThrowArgumentOutOfRange(int precision)
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => CoverageMath.FormatDelta(1m, precision, RoundingMode.Down)
    ).ParamName.ShouldBe("precision");

    Should.Throw<ArgumentOutOfRangeException>(
      () => CoverageMath.FormatDelta(null, precision, RoundingMode.Down)
    ).ParamName.ShouldBe("precision");
  }

  private static decimal Parse(string value)
  {
    return decimal.Parse(value, CultureInfo.InvariantCulture);
  }

  #endregion
}
