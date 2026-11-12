// ------------------------------------------------------------------------------------------------
// <copyright file="CcSparkline.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A small trend line drawn in SVG on the server, without axis: its accessible name carries the values it draws, the
///   colour is never the only signal. A missing value is skipped.
/// </summary>
public partial class CcSparkline
{
  #region Fields

  private const int Width = 110;

  private const int Height = 30;

  private const double Margin = 0.2;

  private double _min;

  private double _max;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the values, the oldest first; a <see langword="null" /> one is skipped.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<double?> Values { get; set; } = [];

  /// <summary>Gets or sets the accessible name, which should say the first and last values.</summary>
  [Parameter, EditorRequired]
  public string AriaLabel { get; set; } = null!;

  /// <summary>Gets or sets additional CSS classes.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  private string CssClass => "cc-spark" + CcCss.Extra(Class);

  private IReadOnlyList<double> Points { get; set; } = [];

  private string LinePath => Path();

  private string AreaPath => Path() + $" L{Num(X(Points.Count - 1))} {Height} L{Num(X(0))} {Height} Z";

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    Points = [.. Values.OfType<double>()];
    (_min, _max) = Points.Count == 0 ? (0d, 1d) : (Points.Min() - Margin, Points.Max() + Margin);
  }

  #endregion

  #region Methods

  private static string Num(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

  private double X(int index) => Points.Count <= 1 ? Width / 2d : (index / (double)(Points.Count - 1) * (Width - 8)) + 2;

  private double Y(double value) => Height - 3 - ((value - _min) / (_max - _min) * (Height - 6));

  private string Path()
  {
    var path = new StringBuilder();

    for (var i = 0; i < Points.Count; i++)
    {
      path.Append(i == 0 ? 'M' : 'L').Append(Num(X(i))).Append(' ').Append(Num(Y(Points[i]))).Append(' ');
    }

    return path.ToString().TrimEnd();
  }

  #endregion
}
