// ------------------------------------------------------------------------------------------------
// <copyright file="AccountTokens.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Tokens;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The personal tokens of the API of the signed-in user: created for one organization, with the instance scope
///   offered to the administrators of the instance only, and revoked. The value of a created token is shown once.
/// </summary>
public partial class AccountTokens
{
  #region Fields

  private readonly LatestRequest _latest = new();

  private bool _creating;

  private bool _showRevoked;

  private RevokedToken? _revoking;

  private IssuedTokenViewModel? _issued;

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private AccountTokensViewModel? Account { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  private bool HasRevoked => Account?.Tokens.Any(static t => t.IsRevoked) ?? false;

  private IReadOnlyList<ApiTokenViewModel> Shown =>
    Account is null ? [] : _showRevoked ? Account.Tokens : [.. Account.Tokens.Where(static t => !t.IsRevoked)];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnInitializedAsync()
  {
    Crumbs = [new CcBreadcrumbItem(L["Nav_Account"])];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private static string Date(DateTimeOffset at) => at.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

  private async Task LoadAsync()
  {
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync(static (sender, ct) => sender.ListApiTokenViewModelsAsync(ct)).ConfigureAwait(true);

    // Another load started since: its own answer shows the list.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Account) = result switch
    {
      { Succeeded: true, Value: { } account } => (PageState.Ready, account),
      var _ => (PageState.Failed, Account),
    };
  }

  private static Task<bool> RevokeAsync(ISender sender, long tokenId, CancellationToken cancellationToken) =>
    sender.RevokeApiTokenAsync(tokenId, cancellationToken);

  private void ToggleRevoked(ChangeEventArgs e) => _showRevoked = e.Value is true;

  private async Task ShowIssuedAsync(IssuedTokenViewModel issued)
  {
    (_creating, _issued) = (false, issued);

    await LoadAsync().ConfigureAwait(true);
  }

  // The value is dropped with the dialog: nothing can show it again.
  private void ForgetIssued() => _issued = null;

  private async Task RevokedAsync()
  {
    _revoking = null;

    await LoadAsync().ConfigureAwait(true);
  }

  #endregion
}
