// ------------------------------------------------------------------------------------------------
// <copyright file="IssuedTokenDialog.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>
///   Shows the value of a token that was just created, this one time. Closing it forgets the value: the page that
///   opened it drops it, and nothing can show it again. An imported token never opens this dialog, since its value is
///   the one the user already holds.
/// </summary>
public partial class IssuedTokenDialog
{
  #region Parameters

  /// <summary>Gets or sets the token just created, or <see langword="null" /> while the dialog is closed.</summary>
  [Parameter]
  public IssuedTokenViewModel? Token { get; set; }

  /// <summary>Gets or sets what happens when the dialog closes: the page forgets the value.</summary>
  [Parameter]
  public EventCallback OnClosed { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Methods

  private Task CloseAsync() => OnClosed.InvokeAsync();

  #endregion
}
