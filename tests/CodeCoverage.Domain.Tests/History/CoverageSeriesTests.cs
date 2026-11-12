// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageSeriesTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.History;

namespace CodeCoverage.Domain.Tests.History;

public sealed class CoverageSeriesTests
{
  #region Fields

  // A Wednesday.
  private static readonly DateTimeOffset Wednesday = new(2026, 11, 11, 15, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Theory]
  [InlineData("2026-11-11T15:00:00Z", "2026-11-09T00:00:00Z")]
  [InlineData("2026-11-09T00:00:00Z", "2026-11-09T00:00:00Z")]
  [InlineData("2026-11-15T23:59:59Z", "2026-11-09T00:00:00Z")]
  [InlineData("2026-11-16T00:00:00Z", "2026-11-16T00:00:00Z")]
  [InlineData("2026-11-16T01:00:00+02:00", "2026-11-09T00:00:00Z")]
  public void PeriodOf_Week_ShouldStartOnMondayInUtc(string at, string expected) =>
    CoverageSeries.PeriodOf(DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture), HistoryInterval.Week)
                  .ShouldBe(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));

  [Fact]
  public void PeriodOf_Day_ShouldBeTheUtcDate() =>
    CoverageSeries.PeriodOf(new DateTimeOffset(2026, 11, 12, 1, 0, 0, TimeSpan.FromHours(3)), HistoryInterval.Day)
                  .ShouldBe(new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero));

  [Fact]
  public void Build_ShouldGiveOneSeriesPerFlagInTheOrderAskedEvenEmpty()
  {
    var series = CoverageSeries.Build([Entry(1, "unit", Wednesday, 50m)], ["", "unit", "e2e"], HistoryInterval.Commit, Wednesday.AddDays(-1), Wednesday.AddDays(1));

    series.Select(static s => s.Flag).ShouldBe(["", "unit", "e2e"]);
    series[1].Points.ShouldHaveSingleItem().Coverage.ShouldBe(50m);
    series[0].Points.ShouldBeEmpty();
    series[2].Points.ShouldBeEmpty();
  }

  // Flags are case-sensitive.
  [Fact]
  public void Build_FlagOfAnotherCase_ShouldNotMatch() =>
    CoverageSeries.Build([Entry(1, "Unit", Wednesday, 50m)], ["unit"], HistoryInterval.Commit, Wednesday.AddDays(-1), Wednesday.AddDays(1))
                  .ShouldHaveSingleItem().Points.ShouldBeEmpty();

  // The range applies to the date of the point, the committer first.
  [Fact]
  public void Build_ShouldFilterOnTheDateOfThePoint()
  {
    var committedBefore = Entry(1, "", Wednesday, 50m) with { CommittedAt = Wednesday.AddDays(-5) };

    CoverageSeries.Build([committedBefore], [""], HistoryInterval.Commit, Wednesday.AddDays(-1), Wednesday.AddDays(1))
                  .ShouldHaveSingleItem().Points.ShouldBeEmpty();
  }

  // Two commits with the same date: the one of the highest identifier wins, the same way on both engines.
  [Fact]
  public void Build_SameDate_ShouldKeepTheLatestCommit()
  {
    var series = CoverageSeries.Build([Entry(8, "", Wednesday, 80m), Entry(7, "", Wednesday, 70m)], [""], HistoryInterval.Day, Wednesday.AddDays(-1), Wednesday.AddDays(1));

    series.ShouldHaveSingleItem().Points.ShouldHaveSingleItem().Coverage.ShouldBe(80m);
  }

  private static HistoryEntry Entry(long commitId, string flag, DateTimeOffset completedAt, decimal coverage) =>
    new(commitId, GitSha.Parse(new string((char)('a' + (commitId % 6)), GitSha.Length)), flag, completedAt, null, 10, 5, 5, 0, coverage);

  #endregion
}
