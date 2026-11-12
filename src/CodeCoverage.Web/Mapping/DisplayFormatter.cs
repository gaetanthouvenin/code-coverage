// ------------------------------------------------------------------------------------------------
// <copyright file="DisplayFormatter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.Localization;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Web.Mapping;

/// <summary>
///   The one place where the pages write a coverage, a change of coverage and a SHA: the numbers are rounded as the
///   comments and statuses round them (<see cref="CoverageMath" />), then written with the separators of the culture.
/// </summary>
internal static class DisplayFormatter
{
  #region Fields

  /// <summary>What a page shows where a value is missing.</summary>
  public const string Dash = "—";

  /// <summary>The length of a short SHA.</summary>
  public const int ShortShaLength = 7;

  #endregion

  #region Methods

  /// <summary>Writes a coverage with its percent sign, as the culture writes it.</summary>
  /// <param name="value">The coverage in percent, or <see langword="null" />.</param>
  /// <param name="precision">The number of decimals.</param>
  /// <param name="rounding">How the value is rounded.</param>
  /// <returns>The text, or <see cref="Dash" /> for <see langword="null" />.</returns>
  public static string Percent(decimal? value, int precision, RoundingMode rounding)
  {
    if (value is null)
    {
      return Dash;
    }

    var culture = CultureInfo.CurrentCulture;
    var number = Localize(CoverageMath.Format(value, precision, rounding), culture);

    // A culture whose percent pattern is « n % » (French) separates the sign with a non-breaking space.
    return culture.NumberFormat.PercentPositivePattern is 0 ? number + " %" : number + "%";
  }

  /// <summary>Writes a number of points, without sign, as the culture writes it.</summary>
  /// <param name="value">The number.</param>
  /// <param name="precision">The number of decimals.</param>
  /// <param name="rounding">How the value is rounded.</param>
  /// <returns>The text.</returns>
  public static string Number(decimal value, int precision, RoundingMode rounding) =>
    Localize(CoverageMath.Format(value, precision, rounding), CultureInfo.CurrentCulture);

  /// <summary>Writes a change of coverage with its sign, as the culture writes it.</summary>
  /// <param name="value">The change in points, or <see langword="null" />.</param>
  /// <param name="precision">The number of decimals.</param>
  /// <param name="rounding">How the value is rounded.</param>
  /// <returns>The text, or <see langword="null" /> when there is no change to show.</returns>
  public static string? Delta(decimal? value, int precision, RoundingMode rounding) =>
    value is null ? null : Localize(CoverageMath.FormatDelta(value, precision, rounding), CultureInfo.CurrentCulture);

  /// <summary>Writes the seven first characters of a SHA.</summary>
  /// <param name="sha">The SHA.</param>
  /// <returns>The short SHA.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  public static string ShortSha(GitSha sha)
  {
    ArgumentNullException.ThrowIfNull(sha);

    return sha.Value[..ShortShaLength];
  }

  /// <summary>Writes how long ago something happened: minutes, hours, then days.</summary>
  /// <param name="at">When it happened.</param>
  /// <param name="now">The current time.</param>
  /// <param name="localizer">The texts of the console.</param>
  /// <returns>The age, e.g. « 12 min ago ».</returns>
  /// <exception cref="ArgumentNullException"><paramref name="localizer" /> is <see langword="null" />.</exception>
  public static string Age(DateTimeOffset at, DateTimeOffset now, IStringLocalizer localizer)
  {
    ArgumentNullException.ThrowIfNull(localizer);

    var age = now - at;

    return age switch
    {
      { TotalMinutes: < 1 } => localizer["Age_JustNow"],
      { TotalHours: < 1 } => localizer["Age_Minutes", (int)age.TotalMinutes],
      { TotalDays: < 1 } => localizer["Age_Hours", (int)age.TotalHours],
      var _ => localizer["Age_Days", (int)age.TotalDays],
    };
  }

  /// <summary>Replaces the invariant decimal point of a formatted number by the one of the culture.</summary>
  /// <param name="invariant">The number, as <see cref="CoverageMath" /> writes it.</param>
  /// <param name="culture">The culture.</param>
  /// <returns>The number in the culture.</returns>
  private static string Localize(string invariant, CultureInfo culture) =>
    invariant.Replace(".", culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);

  #endregion
}
