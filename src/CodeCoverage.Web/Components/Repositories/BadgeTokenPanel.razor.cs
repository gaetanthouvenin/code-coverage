// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeTokenPanel.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The badge token of a repository: the value its README snippet carries, with the buttons that renew it or import
///   the graphing token of Codecov. A public repository serves its badge without a token, and says so.
/// </summary>
public partial class BadgeTokenPanel
{
  #region Fields

  private bool _importing;

  private bool _busy;

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

  /// <summary>Gets or sets the badge token, or <see langword="null" /> for a public repository.</summary>
  [Parameter]
  public string? Token { get; set; }

  /// <summary>Gets or sets what happens when the token changed on the server: the page reads it again.</summary>
  [Parameter]
  public EventCallback OnChanged { get; set; }

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

  private Task<bool> ImportAsync(ISender sender, string label, string value, CancellationToken cancellationToken) =>
    sender.ImportBadgeTokenAsync(Forge, Owner, Name, value, cancellationToken);

  private async Task RotateAsync()
  {
    var (forge, owner, name) = (Forge, Owner, Name);
    _busy = true;

    try
    {
      var result = await Runner.RunAsync((sender, ct) => sender.RotateBadgeTokenAsync(forge, owner, name, ct)).ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, L["BadgeToken_Renewed"], L["BadgeToken_UpdateReadme"]);
        await OnChanged.InvokeAsync().ConfigureAwait(true);
      }
    }
    finally
    {
      _busy = false;
    }
  }

  private async Task ImportedAsync()
  {
    _importing = false;

    await OnChanged.InvokeAsync().ConfigureAwait(true);
  }

  #endregion
}
