// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettings.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Settings;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The settings of one organization (the four keys of <c>SettingScope.Organization</c>, never a secret), in one panel as
///   the validated mockup: reserved to the administrators of the organization. A participant sees the denied state; a
///   non-member, an unknown organization or a forge the console does not serve see the inaccessible state.
/// </summary>
public partial class OrganizationSettings
{
  #region Fields

  private readonly LatestRequest _latest = new();

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
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private IReadOnlyList<SettingViewModel>? Settings { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(Login), new CcBreadcrumbItem(L["Nav_Organization"]), new CcBreadcrumbItem(L["Tab_OrgSettings"])];

    // The editors of the previous organization go at once: none of them may write to the new one.
    State = PageState.Loading;
    Settings = null;

    return LoadAsync();
  }

  #endregion

  #region Methods

  private async Task LoadAsync()
  {
    var (forge, login) = (Forge, Login);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetOrganizationSettingViewModelsAsync(forge, login, ct)).ConfigureAwait(true);

    // Another organization was opened since: its own load shows it.
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

  // The editor sends the commands of this organization, never the ones of the instance.
  private Task<bool> SaveAsync(ISender sender, string key, string value, CancellationToken cancellationToken) =>
    sender.UpdateOrganizationSettingAsync(Forge, Login, key, value, cancellationToken);

  private Task<bool> ResetAsync(ISender sender, string key, CancellationToken cancellationToken) =>
    sender.ResetOrganizationSettingAsync(Forge, Login, key, cancellationToken);

  private void Deny() => (State, Settings) = (PageState.Denied, null);

  private void Lose() => (State, Settings) = (PageState.Missing, null);

  // The organization is part of the key: another organization rebuilds every editor.
  private string EditorKey(SettingViewModel setting) => $"{Forge}/{Login}/{setting.Key}";

  #endregion
}
