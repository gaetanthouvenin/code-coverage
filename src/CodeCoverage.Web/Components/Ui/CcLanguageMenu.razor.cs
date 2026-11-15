// ------------------------------------------------------------------------------------------------
// <copyright file="CcLanguageMenu.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The round <c>globe</c> button of the public bar, with the code of the language in a chip, that opens the
///   <see cref="CcLanguagePicker" /> the way the user menu opens.
/// </summary>
public partial class CcLanguageMenu
{
  #region Fields

  private readonly string _panelId = $"cc-lang-menu-{Guid.NewGuid():N}";

  private bool _open;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the language of the console now.</summary>
  private static ConsoleLanguage Current => ConsoleLanguages.Current;

  /// <summary>Gets the accessible name of the button: « Language (Italiano) ».</summary>
  private string Label => $"{L["Language_Label"]} ({Current.NativeName})";

  #endregion

  #region Methods

  private void Toggle() => _open = !_open;

  private void OnKeyDown(KeyboardEventArgs args)
  {
    if (args.Key == "Escape")
    {
      _open = false;
    }
  }

  #endregion
}
