// ------------------------------------------------------------------------------------------------
// <copyright file="CcCoverageBar.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A coverage drawn as a bar of hits, partials and misses, followed by its value as text: the bar is hidden from
///   assistive technologies, which read the text. Without any line, only the text is shown.
/// </summary>
public partial class CcCoverageBar
{
  #region Parameters

  /// <summary>Gets or sets the share of hits: a count of lines, or a percent.</summary>
  [Parameter]
  public double Hits { get; set; }

  /// <summary>Gets or sets the share of partials, in the same unit as <see cref="Hits" />.</summary>
  [Parameter]
  public double Partials { get; set; }

  /// <summary>Gets or sets the share of misses, in the same unit as <see cref="Hits" />.</summary>
  [Parameter]
  public double Misses { get; set; }

  /// <summary>Gets or sets the value written after the bar, already formatted.</summary>
  [Parameter, EditorRequired]
  public string Text { get; set; } = null!;

  /// <summary>Gets or sets additional CSS classes.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  private string CssClass => "cc-covbar" + CcCss.Extra(Class);

  private double Total => Math.Max(Hits, 0) + Math.Max(Partials, 0) + Math.Max(Misses, 0);

  #endregion

  #region Methods

  private string WidthOf(double share) => (Math.Max(share, 0) / Total * 100).ToString("0.##", CultureInfo.InvariantCulture);

  #endregion
}
