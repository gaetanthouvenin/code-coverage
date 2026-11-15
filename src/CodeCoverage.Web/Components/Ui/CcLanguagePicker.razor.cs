// ------------------------------------------------------------------------------------------------
// <copyright file="CcLanguagePicker.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Configuration.Localization;
using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>The group of the languages of the console, each named in itself, the current one highlighted (ADR-015).</summary>
/// <remarks>
///   Each language is a link to <c>/culture</c>, which remembers it in the culture cookie and reloads the current page,
///   address and query kept. The names of the languages come from <see cref="ConsoleLanguages" /> and are never
///   translated.
/// </remarks>
public partial class CcLanguagePicker
{
  #region Parameters

  /// <summary>Gets or sets where the picker is drawn. Defaults to <see cref="CcLanguagePickerVariant.Menu" />.</summary>
  [Parameter]
  public CcLanguagePickerVariant Variant { get; set; } = CcLanguagePickerVariant.Menu;

  /// <summary>
  ///   Gets or sets the page to come back to, path and query from the root of the site; <see langword="null" /> keeps the
  ///   current page. A page whose query carries an address from outside (the sign-in page) passes its checked form.
  /// </summary>
  [Parameter]
  public string? ReturnUrl { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the group.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the language of the console now.</summary>
  private static ConsoleLanguage Current => ConsoleLanguages.Current;

  /// <summary>Gets the CSS classes of the group.</summary>
  private string CssClass =>
    Variant switch
    {
      CcLanguagePickerVariant.Row => "cc-lang cc-lang--row",
      CcLanguagePickerVariant.Inline => "cc-lang cc-lang--inline",
      _ => "cc-lang",
    } + CcCss.Extra(Class);

  #endregion

  #region Methods

  /// <summary>Builds the address that chooses a language and comes back to the current page.</summary>
  /// <param name="language">The language.</param>
  /// <returns>The address, relative to the base of the site.</returns>
  private string HrefOf(ConsoleLanguage language) =>
    CultureEndpoints.CultureUri(language.Code, ReturnUrl ?? new Uri(Navigation.Uri).PathAndQuery);

  #endregion
}
