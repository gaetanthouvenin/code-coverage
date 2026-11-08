// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageRangeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

// The cases come from CoverageRangeSchemaField of Codecov's libs/shared/shared/validation/helpers.py.
public sealed class CoverageRangeTests
{
  #region Methods

  [Theory]
  [InlineData("60..80", 60, 80)]
  [InlineData("30...99", 30, 99)]
  [InlineData("60.5...80.25", 60.5, 80.25)]
  [InlineData("0..100", 0, 100)]
  [InlineData("70..70", 70, 70)]
  public void Read_Text_ShouldReadBothBounds(string text, decimal low, decimal high)
  {
    var range = CoverageRange.Read(Text(text));

    range.ShouldNotBeNull();
    range.Low.ShouldBe(low);
    range.High.ShouldBe(high);
  }

  [Theory]
  [InlineData("60")]
  [InlineData("60....80")]
  [InlineData("60..70..80")]
  [InlineData("a..b")]
  [InlineData("60..")]
  [InlineData("80..60")]
  [InlineData("-1..50")]
  [InlineData("50..101")]
  public void Read_InvalidText_ShouldBeNull(string text)
  {
    CoverageRange.Read(Text(text)).ShouldBeNull();
  }

  [Fact]
  public void Read_List_ShouldSortTheBounds()
  {
    var range = CoverageRange.Read(List(Number(80), Number(60)));

    range.ShouldNotBeNull();
    range.Low.ShouldBe(60);
    range.High.ShouldBe(80);
  }

  [Fact]
  public void Read_ListOfNumericTexts_ShouldReadBothBounds()
  {
    CoverageRange.Read(Texts("60", "80")).ShouldNotBeNull();
  }

  [Fact]
  public void Read_ListOfThree_ShouldBeNull()
  {
    CoverageRange.Read(List(Number(1), Number(2), Number(3))).ShouldBeNull();
  }

  [Fact]
  public void Read_Number_ShouldBeNull()
  {
    CoverageRange.Read(Number(60)).ShouldBeNull();
  }

  #endregion
}
