// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeSvgRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security;

using CodeCoverage.Api.Contracts.Badges;

namespace CodeCoverage.Api.Rendering;

/// <summary>Draws a badge with the « flat » template of shields.io, written by hand.</summary>
internal static class BadgeSvgRenderer
{
  #region Fields

  /// <summary>The background of the label.</summary>
  public const string LabelColor = "#555";

  /// <summary>The space around each text, in pixels.</summary>
  private const int Padding = 10;

  #endregion

  #region Methods

  /// <summary>Draws a badge.</summary>
  /// <param name="badge">The texts and the color of the badge.</param>
  /// <returns>The SVG document.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="badge" /> is <see langword="null" />.</exception>
  public static string Render(BadgeResponse badge)
  {
    ArgumentNullException.ThrowIfNull(badge);

    var labelWidth = VerdanaWidthTable.Measure(badge.Label) + Padding;
    var messageWidth = VerdanaWidthTable.Measure(badge.Message) + Padding;
    var width = labelWidth + messageWidth;
    var label = SecurityElement.Escape(badge.Label);
    var message = SecurityElement.Escape(badge.Message);
    var color = SecurityElement.Escape(badge.Color);
    var labelX = Center(0, labelWidth);
    var messageX = Center(labelWidth, messageWidth);

    return string.Create(
      CultureInfo.InvariantCulture,
      $"""
       <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="20" role="img" aria-label="{label}: {message}">
         <title>{label}: {message}</title>
         <linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>
         <clipPath id="r"><rect width="{width}" height="20" rx="3" fill="#fff"/></clipPath>
         <g clip-path="url(#r)"><rect width="{labelWidth}" height="20" fill="{LabelColor}"/><rect x="{labelWidth}" width="{messageWidth}" height="20" fill="{color}"/><rect width="{width}" height="20" fill="url(#s)"/></g>
         <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
           <text x="{labelX}" y="15" fill="#010101" fill-opacity=".3">{label}</text><text x="{labelX}" y="14">{label}</text>
           <text x="{messageX}" y="15" fill="#010101" fill-opacity=".3">{message}</text><text x="{messageX}" y="14">{message}</text>
         </g>
       </svg>
       """
    );
  }

  private static string Center(int start, int width) => (start + (width / 2.0)).ToString("0.#", CultureInfo.InvariantCulture);

  #endregion
}
