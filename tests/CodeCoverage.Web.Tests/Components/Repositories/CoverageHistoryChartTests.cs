// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageHistoryChartTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.History;
using CodeCoverage.Web.Components.Repositories;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Tests.Components.Repositories;

// The chart of history of the overview (M3.22) against a fake sender.
public sealed class CoverageHistoryChartTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<GetCoverageHistoryQuery> _queries = [];

  #endregion

  #region Ctors

  public CoverageHistoryChartTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

    A.CallTo(() => _sender.Send(A<GetCoverageHistoryQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<GetCoverageHistoryQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<IReadOnlyList<CoverageSeries>>([.. query.Flags.Select(static f => new CoverageSeries(f, [Point(Now.AddDays(-2), 80), Point(Now, 81)]))]);
      });
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldDrawTheProjectAndEveryFlagOfTheDefaultBranchOver30Days()
  {
    var cut = Render();

    cut.FindAll("g[data-series]").Select(static g => g.GetAttribute("data-series")).ShouldBe([L("Flag_Project"), "backend", "frontend"]);
    var query = _queries.ShouldHaveSingleItem();
    query.Branch.ShouldBe("main");
    query.Flags.ShouldBe(["", "backend", "frontend"]);
    query.Interval.ShouldBe(GetCoverageHistoryQuery.DayInterval);
    query.From.ShouldBe(Now.AddDays(-30));
    query.To.ShouldBe(Now);
  }

  [Fact]
  public void ChangeFlags_ShouldReloadSeries()
  {
    var cut = Render();

    cut.Find("input[data-flag=backend]").Change(false);

    _queries.Count.ShouldBe(2);
    _queries[^1].Flags.ShouldBe(["", "frontend"]);
    cut.FindAll("g[data-series]").Select(static g => g.GetAttribute("data-series")).ShouldBe([L("Flag_Project"), "frontend"]);
  }

  // 365 days read one point a week, so that 20 flags stay under the 10 000 rows of a history.
  [Fact]
  public void ChangeRange_ToAYear_ShouldReadWeeks()
  {
    var cut = Render();

    cut.FindAll("[role=tab]").Last().Click();

    _queries[^1].Interval.ShouldBe(GetCoverageHistoryQuery.WeekInterval);
    _queries[^1].From.ShouldBe(Now.AddDays(-365));
  }

  [Fact]
  public void ChangeBranch_ShouldReload()
  {
    var cut = Render();

    cut.Find("select").Change("develop");

    _queries[^1].Branch.ShouldBe("develop");
  }

  // At 20 series, the other boxes are disabled: a history reads 20 flags at most.
  [Fact]
  public void Render_MoreThanTwentyFlags_ShouldDisableTheBoxesBeyondTheLimit()
  {
    var flags = Enumerable.Range(1, 25).Select(static i => $"flag{i:00}").ToArray();

    var cut = Render(flags);

    _queries.ShouldHaveSingleItem().Flags.Count.ShouldBe(GetCoverageHistoryQuery.MaxFlags);
    cut.FindAll("input[type=checkbox]:disabled").Count.ShouldBe(6);
  }

  [Fact]
  public void Render_NoPoint_ShouldShowTheEmptyState()
  {
    A.CallTo(() => _sender.Send(A<GetCoverageHistoryQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<CoverageSeries>>([new CoverageSeries("", [])]));

    var cut = Render();

    cut.Find("[data-state=empty]").TextContent.ShouldContain(L("History_EmptyTitle"));
  }

  // The values of the chart are written as the cards write them: precision and rounding of the configuration, and the
  // separators and percent pattern of the culture.
  [Fact]
  public void Render_InFrenchWithPrecision2_ShouldFormatAsTheCards()
  {
    A.CallTo(() => _sender.Send(A<GetCoverageHistoryQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<CoverageSeries>>([new CoverageSeries("", [Point(Now.AddDays(-1), 80), Point(Now, 81.456m)])]));
    var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      var cut = Render(new CoverageFormatViewModel(2, "Down"), "backend");

      var label = cut.Find(".cc-chart__label").TextContent;
      label.ShouldStartWith(L("Flag_Project") + " 81,45");
      label.ShouldEndWith(" %");
      cut.Find("svg[role=img]").GetAttribute("aria-label")!.ShouldContain("81,45 %");
      cut.FindAll("table.cc-sr-only td").Select(static td => td.TextContent).ShouldBe(["80,00 %", "81,45 %"]);
    }
    finally
    {
      (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
    }
  }

  // A change of branch while the first load is pending: the late answer of the first is dropped.
  [Fact]
  public void ChangeBranch_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<IReadOnlyList<CoverageSeries>>();
    A.CallTo(() => _sender.Send(A<GetCoverageHistoryQuery>.That.Matches(q => q.Branch == "main"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<CoverageSeries>>(first.Task));
    var cut = Render();

    cut.Find("select").Change("develop");
    cut.WaitForAssertion(() => cut.FindAll("g[data-series]").Count.ShouldBe(3));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult([new CoverageSeries("", [Point(Now, 50)])]));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll("g[data-series]").Count.ShouldBe(3);
  }

  private static CoveragePoint Point(DateTimeOffset at, decimal coverage) => new(at, GitSha.Parse(new string('a', GitSha.Length)), 100, 80, 20, 0, coverage);

  private IRenderedComponent<CoverageHistoryChart> Render(params string[] flags) => Render(new CoverageFormatViewModel(1, "Nearest"), flags);

  private IRenderedComponent<CoverageHistoryChart> Render(CoverageFormatViewModel format, params string[] flags) =>
    Render<CoverageHistoryChart>(p => p.Add(static c => c.Forge, "github")
                                       .Add(c => c.Format, format)
                                       .Add(static c => c.Owner, "octo-org")
                                       .Add(static c => c.Name, "app")
                                       .Add(static c => c.DefaultBranch, "main")
                                       .Add(static c => c.Branches, ["develop", "main"])
                                       .Add(c => c.Flags, flags.Length == 0 ? ["backend", "frontend"] : flags));

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
