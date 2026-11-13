// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationUploadTokens.razor.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The upload tokens of an organization, valid for every repository it owns: created, imported from Codecov, and
///   revoked. Reserved to the administrators of the organization; a participant sees the denied state, and a
///   non-member the inaccessible one. A banner warns while a rotation is advised, since a repository that left the
///   organization may still hold one of these tokens.
/// </summary>
public partial class OrganizationUploadTokens
{
  #region Fields

  private readonly LatestRequest _latest = new();

  private bool _creating;

  private bool _importing;

  private UploadTokenViewModel? _revoking;

  private IssuedTokenViewModel? _issued;

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

  private IReadOnlyList<UploadTokenViewModel>? Tokens { get; set; }

  private bool RotationAdvised { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(Login), new CcBreadcrumbItem(L["Nav_Organization"]), new CcBreadcrumbItem(L["Tab_UploadTokens"])];

    // The tokens of the previous organization go at once: none of them belongs to the new one.
    State = PageState.Loading;
    Tokens = null;

    return LoadAsync();
  }

  #endregion

  #region Methods

  private async Task LoadAsync()
  {
    var (forge, login) = (Forge, Login);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.ListOrganizationUploadTokenViewModelsAsync(forge, login, ct)).ConfigureAwait(true);

    // Another organization was opened since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Tokens) = result switch
    {
      { Succeeded: true, Value: { } tokens } => (PageState.Ready, tokens.Tokens),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      { Forbidden: true } => (PageState.Denied, null),
      var _ => (PageState.Failed, Tokens),
    };
    RotationAdvised = result is { Succeeded: true, Value.RotationAdvised: true };
  }

  private Task<IssuedTokenViewModel?> CreateAsync(ISender sender, string label, CancellationToken cancellationToken) =>
    sender.CreateOrganizationUploadTokenAsync(Forge, Login, label, cancellationToken);

  private Task<bool> ImportAsync(ISender sender, string label, string value, CancellationToken cancellationToken) =>
    sender.ImportOrganizationUploadTokenAsync(Forge, Login, label, value, cancellationToken);

  private Task<bool> RevokeAsync(ISender sender, long tokenId, CancellationToken cancellationToken) =>
    sender.RevokeOrganizationUploadTokenAsync(Forge, Login, tokenId, cancellationToken);

  private void AskRevoke(UploadTokenViewModel token) => _revoking = token;

  private async Task ShowIssuedAsync(IssuedTokenViewModel issued)
  {
    (_creating, _issued) = (false, issued);

    await LoadAsync().ConfigureAwait(true);
  }

  // The value is dropped with the dialog: nothing can show it again.
  private void ForgetIssued() => _issued = null;

  private async Task ImportedAsync()
  {
    _importing = false;

    await LoadAsync().ConfigureAwait(true);
  }

  private async Task RevokedAsync()
  {
    _revoking = null;

    await LoadAsync().ConfigureAwait(true);
  }

  #endregion
}
