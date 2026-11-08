// ------------------------------------------------------------------------------------------------
// <copyright file="CcCss.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>Builds the CSS class lists shared by the UI components.</summary>
internal static class CcCss
{
  #region Methods

  /// <summary>Gets the extra classes of a component, ready to append to its own classes.</summary>
  /// <param name="classes">The extra classes given by the caller, if any.</param>
  /// <returns>The classes preceded by a space, or an empty string.</returns>
  public static string Extra(string? classes) =>
    string.IsNullOrWhiteSpace(classes) ? string.Empty : " " + classes.Trim();

  /// <summary>Gets the BEM modifier of a tone.</summary>
  /// <param name="tone">The tone.</param>
  /// <returns><c>neutral</c>, <c>info</c>, <c>success</c>, <c>warning</c> or <c>danger</c>.</returns>
  public static string Tone(CcTone tone) =>
    tone switch
    {
      CcTone.Info => "info",
      CcTone.Success => "success",
      CcTone.Warning => "warning",
      CcTone.Danger => "danger",
      var _ => "neutral",
    };

  #endregion
}
