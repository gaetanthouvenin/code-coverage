// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageDetailTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Coverage;

public sealed class CoverageDetailTests
{
  #region Methods

  [Fact]
  public void Ratios_NothingCoverable_ShouldBeNull()
  {
    var detail = new CoverageDetail(0, 0, 0, 0, 0, 0);

    detail.LineRatio.ShouldBeNull();
    detail.BranchRatio.ShouldBeNull();
    detail.MethodRatio.ShouldBeNull();
  }

  [Fact]
  public void Ratios_ShouldUseCoverageMath()
  {
    var detail = new CoverageDetail(3, 4, 1, 3, 2, 2);

    detail.LineRatio.ShouldBe(75m);
    detail.BranchRatio.ShouldBe(33.33333m);
    detail.MethodRatio.ShouldBe(100m);
  }

  [Fact]
  public void MethodRatio_UnknownMethods_ShouldBeNull()
  {
    var detail = new CoverageDetail(1, 1, 0, 0, null, null);

    detail.HasMethods.ShouldBeFalse();
    detail.MethodRatio.ShouldBeNull();
  }

  [Fact]
  public void Add_ShouldSumEveryCounter()
  {
    var sum = new CoverageDetail(3, 4, 1, 3, 2, 2).Add(new CoverageDetail(1, 6, 2, 2, 0, 3));

    sum.ShouldBe(new CoverageDetail(4, 10, 3, 5, 2, 5));
  }

  [Fact]
  public void Add_UnknownMethodsOnOneSide_ShouldMakeThemUnknown()
  {
    var sum = new CoverageDetail(3, 4, 1, 3, 2, 2).Add(new CoverageDetail(1, 6, 2, 2, null, null));

    sum.ShouldBe(new CoverageDetail(4, 10, 3, 5, null, null));
  }

  [Fact]
  public void Add_UnknownMethodsOnTheLeft_ShouldMakeThemUnknown()
  {
    var sum = new CoverageDetail(1, 6, 2, 2, null, null).Add(new CoverageDetail(3, 4, 1, 3, 2, 2));

    sum.ShouldBe(new CoverageDetail(4, 10, 3, 5, null, null));
  }

  [Fact]
  public void Add_BeyondAnInteger_ShouldHoldToTheLargest()
  {
    var sum = new CoverageDetail(1, int.MaxValue, 0, 0, 0, 0).Add(new CoverageDetail(1, 1, 0, 0, 0, 0));

    sum.CoverableLines.ShouldBe(int.MaxValue);
    sum.ExecutedLines.ShouldBe(2);
  }

  [Theory]
  [InlineData(2, 1, 0, 0, 0, 0)]
  [InlineData(0, 0, 3, 2, 0, 0)]
  [InlineData(0, 0, 0, 0, 2, 1)]
  [InlineData(-1, 0, 0, 0, 0, 0)]
  public void Create_InvalidCounter_ShouldThrow(int executed, int coverable, int covered, int branches, int methods, int totalMethods)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => new CoverageDetail(executed, coverable, covered, branches, methods, totalMethods));
  }

  [Fact]
  public void Create_OneMethodCounterOnly_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => new CoverageDetail(0, 0, 0, 0, 1, null));
  }

  #endregion
}
