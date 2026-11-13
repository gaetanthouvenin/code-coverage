// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeTokenDialog.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>Asks before revoking a token, since anything still using it stops working and nothing brings it back.</summary>
public partial class RevokeTokenDialog
{
  #region Fields

  private bool _busy;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the token to revoke, or <see langword="null" /> while the dialog is closed.</summary>
  [Parameter]
  public UploadTokenViewModel? Token { get; set; }

  /// <summary>Gets or sets how the token is revoked: the page knows what it belongs to.</summary>
  [Parameter, EditorRequired]
  public Func<ISender, long, CancellationToken, Task<bool>> Revoke { get; set; } = null!;

  /// <summary>Gets or sets what happens once the token is revoked.</summary>
  [Parameter]
  public EventCallback OnRevoked { get; set; }

  /// <summary>Gets or sets what happens when the dialog closes without revoking anything.</summary>
  [Parameter]
  public EventCallback OnClosed { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Methods

  private async Task RevokeAsync()
  {
    if (Token is not { } token)
    {
      return;
    }

    _busy = true;

    try
    {
      var result = await Runner.RunAsync((sender, ct) => Revoke(sender, token.Id, ct)).ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, L["Token_RevokedToast"], token.Label);
        await OnRevoked.InvokeAsync().ConfigureAwait(true);
      }
    }
    finally
    {
      _busy = false;
    }
  }

  private Task CloseAsync() => OnClosed.InvokeAsync();

  #endregion
}
