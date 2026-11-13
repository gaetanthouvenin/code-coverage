// ------------------------------------------------------------------------------------------------
// <copyright file="CopyBlock.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>
///   A block of text with a button that copies it: a token shown once, a badge snippet. The copy is confirmed in a
///   live region, so that it is announced without moving the focus.
/// </summary>
public partial class CopyBlock
{
  #region Fields

  private string? _announcement;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the text shown and copied.</summary>
  [Parameter, EditorRequired]
  public string Text { get; set; } = null!;

  /// <summary>Gets or sets what the block holds, for assistive technologies.</summary>
  [Parameter, EditorRequired]
  public string Label { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IJSRuntime Js { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Methods

  private async Task CopyAsync()
  {
    // A browser that refuses the clipboard (an insecure origin, a denied permission) leaves the text selectable.
    try
    {
      await Js.InvokeVoidAsync("ccUi.copy", Text).ConfigureAwait(true);
      _announcement = L["Token_Copied"];
    }
    catch (JSException)
    {
      _announcement = L["Token_CopyFailed"];
    }
  }

  #endregion
}
