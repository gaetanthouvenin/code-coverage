// ------------------------------------------------------------------------------------------------
// <copyright file="AdminSettings.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Settings;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The settings of the instance, by group, edited without a restart unless stated otherwise: reserved to the
///   administrators of the instance (the server refuses the others, the page then says so).
/// </summary>
public partial class AdminSettings
{
  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private IReadOnlyList<SettingGroupViewModel>? Groups { get; set; }

  private string? Filter { get; set; }

  private string? ReplacedKey { get; set; }

  // The search keeps the keys that contain it, whatever its case, and the groups that still have one.
  private IEnumerable<SettingGroupViewModel> VisibleGroups =>
    string.IsNullOrWhiteSpace(Filter)
      ? Groups ?? []
      : (Groups ?? []).Select(g => g with { Settings = [.. g.Settings.Where(s => s.Key.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase))] })
                      .Where(static g => g.Settings.Count > 0);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnInitializedAsync() => LoadAsync();

  #endregion

  #region Methods

  private async Task LoadAsync()
  {
    var result = await Runner.RunAsync(static (sender, ct) => sender.GetSettingGroupsViewModelAsync(ct)).ConfigureAwait(true);

    (State, Groups) = result switch
    {
      { Succeeded: true } => (PageState.Ready, result.Value),
      { Forbidden: true } => (PageState.Denied, null),
      var _ => (PageState.Failed, Groups),
    };
  }

  private void OpenReplace(string key) => ReplacedKey = key;

  // A change refused with 403: the user is no longer an administrator of the instance, the page says so.
  private void Deny()
  {
    ReplacedKey = null;
    (State, Groups) = (PageState.Denied, null);
  }

  private async Task OnReplaceClosedAsync(bool replaced)
  {
    ReplacedKey = null;

    if (replaced)
    {
      await LoadAsync().ConfigureAwait(true);
    }
  }

  #endregion
}
