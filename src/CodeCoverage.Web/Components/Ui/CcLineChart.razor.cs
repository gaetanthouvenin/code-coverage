// ------------------------------------------------------------------------------------------------
// <copyright file="CcLineChart.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A line chart drawn in SVG on the server, without script: one line per series, told apart by colour, dash and a
///   direct label (two chart colours are too close for colour-blind readers), with a hidden table of every value. The
///   values are percents: the scale never leaves 0 to 100.
/// </summary>
public partial class CcLineChart
{
  #region Fields

  private const int Width = 900;

  private const int Height = 300;

  private const int Left = 46;

  private const int Right = 150;

  private const int Top = 14;

  private const int Bottom = 30;

  private const int GridStep = 5;

  private const int MinimumSpan = 10;

  private const double Ceiling = 100;

  private const double LabelGap = 15;

  private static readonly string[] Colors = ["var(--cc-chart-1)", "var(--cc-chart-3)", "var(--cc-chart-2)"];

  // Seven dashes for three colours: as 7 and 3 share no divisor, the 21 first series never share both.
  private static readonly string[] Dashes = [string.Empty, "7 4", "2 4", "10 3 2 3", "14 4", "4 2 4 6", "2 2 2 6"];

  private double _min;

  private double _max = 100;

  private DateTimeOffset _first;

  private DateTimeOffset _last;

  private double[] _labelY = [];

  #endregion

  #region Parameters

  /// <summary>Gets or sets the series, each with its label.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<CcChartSeries> Series { get; set; } = [];

  /// <summary>Gets or sets the accessible name of the chart; the last value of each series is added to it.</summary>
  [Parameter, EditorRequired]
  public string AriaLabel { get; set; } = null!;

  /// <summary>Gets or sets the header of the date column of the hidden table.</summary>
  [Parameter]
  public string DateHeader { get; set; } = "Date";

  /// <summary>Gets or sets how a value is written; by default with one decimal in the culture.</summary>
  [Parameter]
  public Func<double, string>? ValueFormat { get; set; }

  /// <summary>Gets or sets how a date is written; by default as a short day and month in the culture.</summary>
  [Parameter]
  public Func<DateTimeOffset, string>? DateFormat { get; set; }

  /// <summary>Gets or sets additional CSS classes.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  private string CssClass => "cc-chart" + CcCss.Extra(Class);

  private IEnumerable<int> GridValues => Enumerable.Range(0, ((int)(_max - _min) / GridStep) + 1).Select(i => (int)_min + (i * GridStep));

  // Every date of every series, the oldest first: the rows of the hidden table.
  private IReadOnlyList<DateTimeOffset> Dates { get; set; } = [];

  // The first, middle and last dates under the axis.
  private IReadOnlyList<DateTimeOffset> AxisDates { get; set; } = [];

  private string Summary
  {
    get
    {
      var values = Series.Select(s => LastOf(s) is { Value: { } v } ? $"{s.Label} {FormatValue(v)}" : null).OfType<string>();

      return string.Join(", ", values) is { Length: > 0 } text ? $"{AriaLabel}: {text}" : AriaLabel;
    }
  }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    var values = Series.SelectMany(static s => s.Points).Where(static p => p.Value is not null).Select(static p => p.Value!.Value).ToList();
    (_min, _max) = values.Count == 0 ? (0d, Ceiling) : (Math.Floor(values.Min() / GridStep) * GridStep, Math.Ceiling(values.Max() / GridStep) * GridStep);
    (_min, _max) = (Math.Clamp(_min, 0, Ceiling), Math.Clamp(_max, 0, Ceiling));

    // A span too narrow is widened upward, or downward from 100: a percent is never drawn above 100.
    if (_max - _min < MinimumSpan)
    {
      (_min, _max) = _min + MinimumSpan <= Ceiling ? (_min, _min + MinimumSpan) : (Ceiling - MinimumSpan, Ceiling);
    }

    Dates = [.. Series.SelectMany(static s => s.Points).Select(static p => p.At).Distinct().Order()];
    (_first, _last) = Dates.Count == 0 ? (default, default) : (Dates[0], Dates[^1]);
    AxisDates = Dates.Count switch
    {
      0 => [],
      1 => [_first],
      2 => [_first, _last],
      var _ => [_first, Dates[Dates.Count / 2], _last],
    };

    PlaceLabels();
  }

  #endregion

  #region Methods

  private static string Num(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

  /// <summary>Gets the colour of a series.</summary>
  /// <param name="index">The position of the series.</param>
  /// <returns>The CSS colour.</returns>
  internal static string ColorOf(int index) => Colors[index % Colors.Length];

  /// <summary>Gets the dash of a series; it cycles at another pace than the colour, so the 21 first series never share both.</summary>
  /// <param name="index">The position of the series.</param>
  /// <returns>The SVG dash array, empty for a solid line.</returns>
  internal static string DashOf(int index) => Dashes[index % Dashes.Length];

  private static CcChartPoint? LastOf(CcChartSeries series) => series.Points.LastOrDefault(static p => p.Value is not null);

  private double X(DateTimeOffset at)
  {
    var span = (_last - _first).TotalSeconds;

    return span <= 0 ? (Left + Width - Right) / 2d : Left + ((at - _first).TotalSeconds / span * (Width - Left - Right));
  }

  private double Y(double value) => Top + ((1 - ((value - _min) / (_max - _min))) * (Height - Top - Bottom));

  private double LabelY(int index) => index < _labelY.Length ? _labelY[index] : 0;

  // A gap in the values starts a new segment of the path: a missing value is never drawn as zero.
  private string PathOf(CcChartSeries series)
  {
    var path = new StringBuilder();
    var move = true;

    foreach (var point in series.Points)
    {
      if (point.Value is not { } value)
      {
        move = true;
        continue;
      }

      path.Append(move ? 'M' : 'L').Append(Num(X(point.At))).Append(' ').Append(Num(Y(value))).Append(' ');
      move = false;
    }

    return path.ToString().TrimEnd();
  }

  // The direct labels are pushed apart so that two series ending close never overlap.
  private void PlaceLabels()
  {
    var ends = Series.Select((s, i) => (Index: i, Y: LastOf(s) is { Value: { } v } ? Y(v) : double.NaN))
                     .Where(static e => !double.IsNaN(e.Y))
                     .OrderBy(static e => e.Y)
                     .ToList();
    _labelY = new double[Series.Count];

    for (var k = 0; k < ends.Count; k++)
    {
      var y = k > 0 ? Math.Max(ends[k].Y, _labelY[ends[k - 1].Index] + LabelGap) : ends[k].Y;
      _labelY[ends[k].Index] = y;
    }
  }

  // Razor reserves the <text> tag: the SVG texts are written through the render tree.
  private static RenderFragment SvgText(double x, double y, string anchor, string cssClass, string content) =>
    builder => WriteText(builder, x, y, anchor, cssClass, content);

  private static void WriteText(RenderTreeBuilder builder, double x, double y, string anchor, string cssClass, string content)
  {
    builder.OpenElement(0, "text");
    builder.AddAttribute(1, "x", Num(x));
    builder.AddAttribute(2, "y", Num(y));
    builder.AddAttribute(3, "text-anchor", anchor);
    builder.AddAttribute(4, "class", cssClass);
    builder.AddContent(5, content);
    builder.CloseElement();
  }

  private string FormatValue(double value) => ValueFormat?.Invoke(value) ?? value.ToString("0.0", CultureInfo.CurrentCulture);

  private string FormatDate(DateTimeOffset at) => DateFormat?.Invoke(at) ?? at.ToString("d MMM", CultureInfo.CurrentCulture);

  private string ValueAt(CcChartSeries series, DateTimeOffset at) =>
    series.Points.FirstOrDefault(p => p.At == at) is { Value: { } value } ? FormatValue(value) : "—";

  #endregion
}
