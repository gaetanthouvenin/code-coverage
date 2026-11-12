// ------------------------------------------------------------------------------------------------
// <copyright file="UiGallerySamples.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The samples of the <c>/_ui</c> gallery, rendered once per theme: KPI cards, badges, buttons,
///   banners, fields, folder tabs, spinners, empty state, error boundary and icons.
/// </summary>
public partial class UiGallerySamples
{
  #region Fields

  private readonly string _panelId = $"cc-gallery-range-{Guid.NewGuid():N}";

  private string? _name = "ci-main";

  private string? _retention = "365";

  private string? _branch = "main";

  private string? _search;

  private string _range = "30";

  private bool _fail;

  private bool _switch = true;

  private bool _dialog;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the name of the theme of the samples (« Light », « Dark »).</summary>
  [Parameter, EditorRequired]
  public string Theme { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the options of the sample retention select.</summary>
  private static IReadOnlyList<CcSelectOption> RetentionOptions { get; } =
  [
    new("90", "90 days"),
    new("365", "1 year"),
    new("730", "2 years"),
  ];

  /// <summary>Gets the options of the sample branch select.</summary>
  private static IReadOnlyList<CcSelectOption> BranchOptions { get; } =
  [
    new("main", "main"),
    new("develop", "develop"),
  ];

  /// <summary>Gets the tabs of the sample range selector.</summary>
  private static IReadOnlyList<CcTabItem> RangeTabs { get; } =
  [
    new("30", "30 days"),
    new("90", "90 days"),
    new("365", "1 year"),
  ];

  /// <summary>Gets every icon of the set.</summary>
  private static IReadOnlyList<CcIconName> Icons { get; } = Enum.GetValues<CcIconName>();

  // Three series over ten days; the second has a gap, which the chart leaves undrawn.
  private static IReadOnlyList<CcChartSeries> ChartSamples { get; } =
  [
    new("Project", [.. Enumerable.Range(0, 10).Select(static d => new CcChartPoint(new DateTimeOffset(2026, 11, 1 + d, 0, 0, 0, TimeSpan.Zero), 80 + (d * 0.25)))]),
    new("backend", [.. Enumerable.Range(0, 10).Select(static d => new CcChartPoint(new DateTimeOffset(2026, 11, 1 + d, 0, 0, 0, TimeSpan.Zero), d == 4 ? (double?)null : 84 + (d * 0.1)))]),
    new("frontend", [.. Enumerable.Range(0, 10).Select(static d => new CcChartPoint(new DateTimeOffset(2026, 11, 1 + d, 0, 0, 0, TimeSpan.Zero), 71 - (d * 0.05)))]),
  ];

  /// <summary>Gets the identifier of the panel of the folder tabs.</summary>
  private string PanelId => _panelId;

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnAfterRender(bool firstRender)
  {
    // The fault has been thrown: « Try again » renders the sample again, healthy this time.
    _fail = false;
  }

  #endregion

  #region Methods

  private void Fail() => _fail = true;

  private void OpenDialog() => _dialog = true;

  private void CloseDialog() => _dialog = false;

  private void ShowSuccessToast() =>
    Toasts.Show(CcTone.Success, "Token revoked", "ci-main can no longer be used.");

  private void ShowErrorToast() =>
    Toasts.Show(CcTone.Danger, "The action could not be completed", "You do not have the permission to do this.");

  #endregion
}
