// ------------------------------------------------------------------------------------------------
// <copyright file="CcLineChartTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcLineChartTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Day = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldDrawOneLineAndOneLabelPerSeries()
  {
    var cut = Render<CcLineChart>(p => p.Add(c => c.Series, [Series("Project", 80, 81, 82), Series("backend", 70, 71, 72)])
                                        .Add(c => c.AriaLabel, "History")
                                        .Add(c => c.ValueFormat, static v => v.ToString("0.0", CultureInfo.InvariantCulture)));

    cut.FindAll("g[data-series]").Select(static g => g.GetAttribute("data-series")).ShouldBe(["Project", "backend"]);
    cut.FindAll("path").Select(static p => p.GetAttribute("stroke-dasharray")).Distinct().Count().ShouldBe(2);
    cut.FindAll(".cc-chart__label").Select(static t => t.TextContent).ShouldBe(["Project 82.0", "backend 72.0"]);
    cut.Find("svg").GetAttribute("aria-label").ShouldBe("History: Project 82.0, backend 72.0");
  }

  // A missing value starts a new segment: it is never drawn as zero.
  [Fact]
  public void Render_Gap_ShouldBreakTheLine()
  {
    var cut = Render<CcLineChart>(p => p.Add(c => c.Series, [Series("Project", 80, null, 82)]).Add(c => c.AriaLabel, "History"));

    var path = cut.Find("path").GetAttribute("d")!;
    path.Count(static c => c == 'M').ShouldBe(2);
    path.ShouldNotContain("L");
  }

  // Every value is also in a table that only assistive technologies read.
  [Fact]
  public void Render_ShouldListEveryValueInAHiddenTable()
  {
    var cut = Render<CcLineChart>(p => p.Add(c => c.Series, [Series("Project", 80, null)])
                                        .Add(c => c.AriaLabel, "History")
                                        .Add(c => c.ValueFormat, static v => v.ToString("0.0", CultureInfo.InvariantCulture))
                                        .Add(c => c.DateFormat, static d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    var table = cut.Find("table.cc-sr-only");
    table.QuerySelector("caption")!.TextContent.ShouldBe("History");
    table.QuerySelectorAll("tbody tr").Select(static r => r.TextContent).ShouldBe(["2026-11-0180.0", "2026-11-02—"]);
  }

  // A percent never leaves 0-100: a span widened to 10 points goes down from 100 rather than above it.
  [Theory]
  [InlineData(new double[] { 100, 100, 100 }, new[] { "90", "95", "100" })]
  [InlineData(new double[] { 96, 98.5, 99.9 }, new[] { "90", "95", "100" })]
  [InlineData(new double[] { 50 }, new[] { "50", "55", "60" })]
  [InlineData(new double[] { 0, 0 }, new[] { "0", "5", "10" })]
  public void Render_ShouldKeepTheScaleBetween0And100(double[] values, string[] grid)
  {
    var cut = Render<CcLineChart>(p => p.Add(c => c.Series, [Series("Project", [.. values.Select(static v => (double?)v)])]).Add(c => c.AriaLabel, "History"));

    cut.FindAll("text[text-anchor=end]").Select(static t => t.TextContent).ShouldBe(grid);
  }

  // The 20 series a history reads are told apart by their colour and dash together.
  [Fact]
  public void Render_TwentySeries_ShouldGiveEachItsOwnColourAndDash()
  {
    var series = Enumerable.Range(0, 20).Select(static i => Series($"flag{i}", 80, 81)).ToArray();

    var cut = Render<CcLineChart>(p => p.Add(c => c.Series, series).Add(c => c.AriaLabel, "History"));

    var styles = cut.FindAll("path").Select(static p => (p.GetAttribute("stroke-dasharray"), p.GetAttribute("style"))).ToList();
    styles.Count.ShouldBe(20);
    styles.Distinct().Count().ShouldBe(20);
  }

  private static CcChartSeries Series(string label, params double?[] values) =>
    new(label, [.. values.Select(static (v, i) => new CcChartPoint(Day.AddDays(i), v))]);

  #endregion
}
