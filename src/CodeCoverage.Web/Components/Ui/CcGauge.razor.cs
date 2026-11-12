// ------------------------------------------------------------------------------------------------
// <copyright file="CcGauge.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>A half-circle gauge of segments drawn in SVG on the server; its accessible name carries the caption and the value.</summary>
public partial class CcGauge
{
  #region Fields

  private const int Segments = 16;

  private const int CenterX = 160;

  private const int CenterY = 156;

  private const int Radius = 116;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the value, in percent from 0 to 100; out of range values are brought back.</summary>
  [Parameter, EditorRequired]
  public double Value { get; set; }

  /// <summary>Gets or sets the caption under the value.</summary>
  [Parameter, EditorRequired]
  public string Caption { get; set; } = null!;

  /// <summary>Gets or sets how the value is written; by default with one decimal and a percent sign.</summary>
  [Parameter]
  public string? ValueText { get; set; }

  /// <summary>Gets or sets additional CSS classes.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  private string CssClass => "cc-gauge" + CcCss.Extra(Class);

  private int Lit => (int)Math.Round(Math.Clamp(Value, 0, 100) / 100 * Segments, MidpointRounding.AwayFromZero);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet() =>
    ValueText ??= Math.Clamp(Value, 0, 100).ToString("0.0", CultureInfo.CurrentCulture) + "%";

  #endregion

  #region Methods

  private static string Num(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

  private static double AngleOf(int index) => 180 - (index * (180d / (Segments - 1)));

  // Razor reserves the <text> tag: the SVG texts are written through the render tree.
  private static RenderFragment SvgText(double y, string cssClass, string? content) =>
    builder => WriteText(builder, y, cssClass, content);

  private static void WriteText(RenderTreeBuilder builder, double y, string cssClass, string? content)
  {
    builder.OpenElement(0, "text");
    builder.AddAttribute(1, "x", Num(CenterX));
    builder.AddAttribute(2, "y", Num(y));
    builder.AddAttribute(3, "text-anchor", "middle");
    builder.AddAttribute(4, "class", cssClass);
    builder.AddContent(5, content);
    builder.CloseElement();
  }

  // The lit segments fade from the accent to its lighter tone; the others take the colour of an empty gauge.
  private string FillOf(int index)
  {
    if (index >= Lit)
    {
      return "var(--cc-gauge-off)";
    }

    var fade = Lit > 1 ? index / (double)(Lit - 1) : 0;

    return string.Create(CultureInfo.InvariantCulture, $"color-mix(in srgb, var(--cc-accent) {Math.Round(100 - (fade * 72))}%, var(--cc-accent-2))");
  }

  #endregion
}
