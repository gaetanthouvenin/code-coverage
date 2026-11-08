// ------------------------------------------------------------------------------------------------
// <copyright file="Target.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Domain.Config;

/// <summary>The coverage a status expects: <c>auto</c> or a percentage.</summary>
public sealed record Target
{
  #region Fields

  private const string AutoText = "auto";

  #endregion

  #region Ctors

  private Target(decimal? percentage)
  {
    Percentage = percentage;
  }

  #endregion

  #region Properties

  /// <summary>Gets the target that follows the coverage of the base.</summary>
  public static Target Auto { get; } = new(percentage: null);

  /// <summary>Gets the expected percentage, or <see langword="null" /> for <see cref="Auto" />.</summary>
  public decimal? Percentage { get; }

  /// <summary>Gets a value indicating whether the target follows the coverage of the base.</summary>
  public bool IsAuto => Percentage is null;

  #endregion

  #region Methods

  /// <summary>Reads a target, as <c>PercentSchemaField</c> of Codecov does.</summary>
  /// <param name="node">The text <c>auto</c>, a number, or a text such as <c>80</c> or <c>80%</c>.</param>
  /// <returns>The target, or <see langword="null" /> if the value is malformed.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="node" /> is null.</exception>
  /// <remarks>Codecov does not bound a target: 150 is valid, and can never be reached.</remarks>
  public static Target? Read(ConfigNode node)
  {
    ArgumentNullException.ThrowIfNull(node);

    if (node.AsText() is AutoText)
    {
      return Auto;
    }

    return ReadPercentage(node) is { } percentage ? new Target(percentage) : null;
  }

  /// <summary>Reads a percentage that cannot be <c>auto</c>.</summary>
  /// <param name="node">A number, or a text such as <c>80</c> or <c>80%</c>.</param>
  /// <returns>The percentage, or <see langword="null" /> if the value is malformed.</returns>
  internal static decimal? ReadPercentage(ConfigNode node) =>
    node.AsNumber()
    ?? (node.AsText() is { Length: > 0 } text && char.IsAsciiDigit(text[0]) ? ParseNumber(text.TrimEnd('%')) : null);

  private static decimal? ParseNumber(string text) =>
    decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
      ? value
      : null;

  #endregion
}
