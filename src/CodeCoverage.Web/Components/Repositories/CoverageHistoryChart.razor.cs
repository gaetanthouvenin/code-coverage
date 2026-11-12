// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageHistoryChart.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The history of the coverage of a repository (<see cref="Ui.CcLineChart" />): a branch, the project and the flags
///   to draw, and a range of 30, 90 or 365 days (one point a day, a week for 365 days).
/// </summary>
public partial class CoverageHistoryChart
{
  #region Fields

  private const string DefaultRange = "30";

  private readonly List<string> _selected = [];

  private readonly LatestRequest _latest = new();

  #endregion

  #region Parameters

  /// <summary>Gets or sets the forge segment of the address.</summary>
  [Parameter, EditorRequired]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner of the repository.</summary>
  [Parameter, EditorRequired]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter, EditorRequired]
  public string Name { get; set; } = null!;

  /// <summary>Gets or sets the branch shown first.</summary>
  [Parameter, EditorRequired]
  public string DefaultBranch { get; set; } = null!;

  /// <summary>Gets or sets the branches of the repository.</summary>
  [Parameter]
  public IReadOnlyList<string> Branches { get; set; } = [];

  /// <summary>Gets or sets the flags of the repository, without the project.</summary>
  [Parameter]
  public IReadOnlyList<string> Flags { get; set; } = [];

  /// <summary>Gets or sets how the repository writes a coverage: the values of the chart are written as the cards.</summary>
  [Parameter, EditorRequired]
  public CoverageFormatViewModel Format { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private TimeProvider Time { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private string SelectedBranch { get; set; } = null!;

  private string SelectedRange { get; set; } = DefaultRange;

  // The project first, then the flags: the order of the check boxes and of the series.
  private IReadOnlyList<string> AllFlags { get; set; } = [];

  private IReadOnlyList<CcChartSeries> ChartSeries { get; set; } = [];

  private IReadOnlyList<CcSelectOption> BranchOptions { get; set; } = [];

  private IReadOnlyList<CcTabItem> RangeTabs { get; set; } = [];

  // A history reads 20 series at most: the other boxes are disabled until one is unchecked.
  private bool IsFull => _selected.Count >= RepositoryMapper.MaxChartFlags;

  private bool HasPoints => ChartSeries.Any(static s => s.Points.Any(static p => p.Value is not null));

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    SelectedBranch = DefaultBranch;
    AllFlags = [string.Empty, .. Flags];
    _selected.Clear();
    _selected.AddRange(AllFlags.Take(RepositoryMapper.MaxChartFlags));
    BranchOptions = [.. Branches.Append(DefaultBranch).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(static b => new CcSelectOption(b, b))];
    RangeTabs =
    [
      new CcTabItem("30", L["History_Days", 30]),
      new CcTabItem("90", L["History_Days", 90]),
      new CcTabItem("365", L["History_Days", 365]),
    ];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private bool IsSelected(string flag) => _selected.Contains(flag, StringComparer.Ordinal);

  private int SeriesIndexOf(string flag) => AllFlags.Where(IsSelected).ToList().IndexOf(flag);

  private string LabelOf(string flag) => flag.Length == 0 ? L["Flag_Project"] : flag;

  private string FormatValue(double value) => RepositoryMapper.FormatCoverage(value, Format);

  private Task ChangeBranchAsync(string? branch)
  {
    SelectedBranch = branch ?? DefaultBranch;

    return LoadAsync();
  }

  private Task ChangeRangeAsync(string range)
  {
    SelectedRange = range;

    return LoadAsync();
  }

  private Task ToggleAsync(string flag, ChangeEventArgs change)
  {
    if (change.Value is true && !IsSelected(flag) && !IsFull)
    {
      _selected.Add(flag);
    }
    else if (change.Value is false)
    {
      _selected.Remove(flag);
    }

    return LoadAsync();
  }

  private async Task LoadAsync()
  {
    var flags = AllFlags.Where(IsSelected).ToList();
    var ticket = _latest.Begin();

    if (flags.Count == 0)
    {
      (State, ChartSeries) = (PageState.Ready, []);
      return;
    }

    State = PageState.Loading;
    var (forge, owner, name, branch) = (Forge, Owner, Name, SelectedBranch);
    var days = int.Parse(SelectedRange, CultureInfo.InvariantCulture);
    var now = Time.GetUtcNow();
    var result = await Runner.RunAsync((sender, ct) => sender.GetCoverageSeriesViewModelsAsync(forge, owner, name, branch, flags, days, now, ct)).ConfigureAwait(true);

    // Another branch, range or flag was chosen since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, ChartSeries) = result.Succeeded ? (PageState.Ready, ToChart(result.Value)) : (PageState.Failed, ChartSeries);
  }

  // Every selected series keeps its place, even empty, so that its colour and dash match its check box.
  private List<CcChartSeries> ToChart(IReadOnlyList<CoverageSeriesViewModel> series) =>
    [.. series.Select(s => new CcChartSeries(LabelOf(s.Flag), [.. s.Points.Select(static p => new CcChartPoint(p.At, (double?)p.Coverage))]))];

  #endregion
}
