// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRepositories.razor.cs" company="Gaëtan Thouvenin">
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
///   Every repository the installation of an organization serves, with its last upload and the switch that enables or
///   disables it. Reserved to the administrators of the organization, who administer its repositories; a participant
///   sees the denied state and a non-member the inaccessible one.
/// </summary>
public partial class OrganizationRepositories
{
  #region Fields

  private readonly LatestRequest _latest = new();

  private string? _search;

  private bool _busy;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the forge segment of the organization.</summary>
  [Parameter]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the login of the organization.</summary>
  [Parameter]
  public string Login { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private TimeProvider Time { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private IReadOnlyList<OrganizationRepositoryViewModel>? Repositories { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  private IReadOnlyList<OrganizationRepositoryViewModel> Shown =>
    Repositories is null
      ? []
      : string.IsNullOrWhiteSpace(_search)
        ? Repositories
        : [.. Repositories.Where(r => r.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(Login), new CcBreadcrumbItem(L["Nav_Organization"]), new CcBreadcrumbItem(L["Tab_OrgRepositories"])];

    // The repositories of the previous organization go at once: none of them belongs to the new one.
    State = PageState.Loading;
    Repositories = null;

    return LoadAsync();
  }

  #endregion

  #region Methods

  private string AgeOf(DateTimeOffset at) => DisplayFormatter.Age(at, Time.GetUtcNow(), L);

  private async Task LoadAsync()
  {
    var (forge, login) = (Forge, Login);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.ListOrganizationRepositoryViewModelsAsync(forge, login, ct)).ConfigureAwait(true);

    // Another organization was opened since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Repositories) = result switch
    {
      { Succeeded: true, Value: { } repositories } => (PageState.Ready, repositories),
      { NotFound: true } => (PageState.Missing, null),
      { Forbidden: true } => (PageState.Denied, null),
      var _ => (PageState.Failed, Repositories),
    };
  }

  private void Search(string? text) => _search = text;

  // The sync only queues the job the webhooks use: the forge is never called in the request.
  private async Task SyncAsync()
  {
    var (forge, login) = (Forge, Login);
    _busy = true;

    try
    {
      var result = await Runner.RunAsync((sender, ct) => sender.SyncOrganizationInstallationAsync(forge, login, ct)).ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, L["OrganizationRepositories_SyncQueued"], login);
      }
    }
    finally
    {
      _busy = false;
    }
  }

  // The state comes back from the server: a refusal leaves the switch where the server says it is.
  private async Task ToggleAsync(OrganizationRepositoryViewModel repository, bool active)
  {
    var (forge, login, name) = (Forge, Login, repository.Name);
    _busy = true;

    try
    {
      var result = await Runner
                        .RunAsync((sender, ct) => active
                                    ? sender.ActivateRepositoryAsync(forge, login, name, ct)
                                    : sender.DeactivateRepositoryAsync(forge, login, name, ct))
                        .ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, active ? L["RepositorySettings_Activated"] : L["RepositorySettings_Deactivated"], name);
      }
    }
    finally
    {
      _busy = false;
    }

    await LoadAsync().ConfigureAwait(true);
  }

  #endregion
}
