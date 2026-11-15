// ------------------------------------------------------------------------------------------------
// <copyright file="ConsoleLanguages.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Web.Localization;

/// <summary>The languages of the console, the one source read by the host, the language picker, the documentation and the tests.</summary>
/// <remarks>
///   English is the reference, the default and the fallback language (ADR-015). A language more is a line here, its two
///   resource files <c>Common.{code}.resx</c> and <c>UiStrings.{code}.resx</c> and, when needed, translated documents.
/// </remarks>
public static class ConsoleLanguages
{
  #region Fields

  /// <summary>The code of the default language, English.</summary>
  public const string DefaultCode = "en";

  #endregion

  #region Properties

  /// <summary>Gets the languages of the console, English first.</summary>
  public static IReadOnlyList<ConsoleLanguage> All { get; } =
  [
    new(DefaultCode, "English"),
    new("fr", "Français"),
    new("it", "Italiano"),
    new("es", "Español"),
  ];

  /// <summary>Gets the codes of the languages of the console, English first.</summary>
  public static IReadOnlyList<string> Codes { get; } = [.. All.Select(static l => l.Code)];

  /// <summary>Gets the language of the user interface of the current culture, English when the console does not speak it.</summary>
  public static ConsoleLanguage Current => Find(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? All[0];

  #endregion

  #region Methods

  /// <summary>Finds a language of the console by its code.</summary>
  /// <param name="code">The two-letter code, for example <c>it</c>; the case is ignored.</param>
  /// <returns>The language, or <see langword="null" /> when the console does not speak it.</returns>
  public static ConsoleLanguage? Find(string? code) =>
    All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

  #endregion
}
