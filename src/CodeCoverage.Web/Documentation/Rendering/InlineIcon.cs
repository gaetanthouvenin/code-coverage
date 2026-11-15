// ------------------------------------------------------------------------------------------------
// <copyright file="InlineIcon.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Components.Ui.Icons;

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>Writes a decorative icon as markup, the same SVG as <see cref="CcIcon" />, for the HTML rendered from Markdown.</summary>
internal static class InlineIcon
{
  #region Methods

  /// <summary>Gets the markup of a decorative icon.</summary>
  /// <param name="name">The icon.</param>
  /// <returns>The <c>svg</c> element, hidden from assistive technologies.</returns>
  public static string Markup(CcIconName name) =>
    "<svg class=\"cc-icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" "
    + "stroke-linejoin=\"round\" focusable=\"false\" aria-hidden=\"true\">" + CcIconPaths.Get(name) + "</svg>";

  #endregion
}
