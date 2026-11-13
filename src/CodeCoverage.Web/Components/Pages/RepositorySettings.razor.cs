// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySettings.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The settings of a repository: its state, and the configuration in force shown read-only. Reserved to the
///   administrators of the repository, which includes the administrators of its organization; a reader sees the denied
///   state, and an unknown, unreadable or foreign repository the inaccessible one.
/// </summary>
public partial class RepositorySettings
{
  #region Fields

  private readonly LatestRequest _latest = new();

  #endregion

  #region Parameters

  /// <summary>Gets or sets the forge segment of the address.</summary>
  [Parameter]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner of the repository.</summary>
  [Parameter]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter]
  public string Name { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private RepositorySettingsViewModel? Settings { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  private bool Busy { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(L["Tab_RepoSettings"])];

    // The settings of the previous repository go at once: none of them speaks for the new one.
    State = PageState.Loading;
    Settings = null;

    return LoadAsync();
  }

  #endregion

  #region Methods

  private async Task LoadAsync()
  {
    var (forge, owner, name) = (Forge, Owner, Name);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetRepositorySettingsViewModelAsync(forge, owner, name, ct)).ConfigureAwait(true);

    // Another repository was opened since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Settings) = result switch
    {
      { Succeeded: true, Value: { } settings } => (PageState.Ready, settings),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      { Forbidden: true } => (PageState.Denied, null),
      var _ => (PageState.Failed, Settings),
    };
  }

  // The state comes back from the server: a refusal leaves the switch where the server says it is.
  private async Task ChangeActivationAsync(bool active)
  {
    var (forge, owner, name) = (Forge, Owner, Name);
    Busy = true;

    try
    {
      var result = await Runner
                        .RunAsync((sender, ct) => active
                                    ? sender.ActivateRepositoryAsync(forge, owner, name, ct)
                                    : sender.DeactivateRepositoryAsync(forge, owner, name, ct))
                        .ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, active ? L["RepositorySettings_Activated"] : L["RepositorySettings_Deactivated"], Name);
      }
    }
    finally
    {
      Busy = false;
    }

    await LoadAsync().ConfigureAwait(true);
  }

  #endregion
}
