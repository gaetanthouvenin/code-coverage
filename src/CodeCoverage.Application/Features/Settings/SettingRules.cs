// ------------------------------------------------------------------------------------------------
// <copyright file="SettingRules.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings;

/// <summary>
///   The rules shared by the settings use cases: finding a setting of the catalog, refusing a value
///   provided by a stronger source, checking a value against the type of its setting and writing it
///   in its canonical form.
/// </summary>
internal static class SettingRules
{
  #region Methods

  /// <summary>Finds the definition of a setting of the instance.</summary>
  /// <param name="key">The configuration key, whatever its case.</param>
  /// <returns>The definition.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
  /// <exception cref="NotFoundException">
  ///   The key is not in the catalog, or its value is set per organization
  ///   (<see cref="SettingErrors.Unknown" />).
  /// </exception>
  public static SettingDefinition FindInstanceOrThrow(string key)
  {
    ArgumentNullException.ThrowIfNull(key);

    var definition = SettingDefinitions.Find(key);

    NotFoundException.ThrowIfNotInstanceSetting(definition, key);

    return definition;
  }

  /// <summary>Reads the effective value of a setting and refuses it when a stronger source provides it.</summary>
  /// <param name="store">The settings store.</param>
  /// <param name="definition">The setting.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The effective value, from the store or the default.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="store" /> or <paramref name="definition" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="UnprocessableEntityException">
  ///   A stronger source provides the value (<see cref="SettingErrors.ProvidedByEnvironment" />).
  /// </exception>
  public static async Task<SettingValue> GetEditableAsync(
    ISettingsStore store,
    SettingDefinition definition,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(definition);

    var current = await store.GetAsync(definition, cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfSettingReadOnly(current);

    return current;
  }

  /// <summary>Checks a value against the type, the bounds and the length limit of its setting.</summary>
  /// <param name="definition">The setting.</param>
  /// <param name="value">The value, as typed.</param>
  /// <returns>
  ///   <see langword="null" /> when the value is accepted; otherwise
  ///   <see cref="SettingErrors.InvalidValue" /> or <see cref="SettingErrors.OutOfRange" />.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="definition" /> or <paramref name="value" /> is <see langword="null" />.
  /// </exception>
  public static string? Check(SettingDefinition definition, string value)
  {
    ArgumentNullException.ThrowIfNull(definition);
    ArgumentNullException.ThrowIfNull(value);

    switch (definition.Type)
    {
      case SettingType.Number:
        if (!TryParseNumber(value, out var number))
        {
          return SettingErrors.InvalidValue;
        }

        return number < definition.Min || number > definition.Max ? SettingErrors.OutOfRange : null;

      case SettingType.Boolean:
        return bool.TryParse(value.Trim(), out _) ? null : SettingErrors.InvalidValue;

      case SettingType.Url:
        return IsHttpUrl(value.Trim()) ? null : SettingErrors.InvalidValue;

      default:
        return value.Length > definition.MaxLength ? SettingErrors.OutOfRange : null;
    }
  }

  /// <summary>Writes an accepted value in its canonical form, with the invariant culture.</summary>
  /// <param name="definition">The setting.</param>
  /// <param name="value">The value, accepted by <see cref="Check" />.</param>
  /// <returns>
  ///   The number or the boolean written with the invariant culture, the URL or the list trimmed,
  ///   the text and the YAML unchanged.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="definition" /> or <paramref name="value" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="FormatException">The value does not match the type of the setting.</exception>
  public static string Normalize(SettingDefinition definition, string value)
  {
    ArgumentNullException.ThrowIfNull(definition);
    ArgumentNullException.ThrowIfNull(value);

    return definition.Type switch
    {
      SettingType.Number => TryParseNumber(value, out var number)
                              ? number.ToString(CultureInfo.InvariantCulture)
                              : throw new FormatException($"'{value}' is not a number."),
      SettingType.Boolean => bool.Parse(value.Trim()) ? "true" : "false",
      SettingType.Url or SettingType.Csv => value.Trim(),
      _ => value,
    };
  }

  /// <summary>Parses an integer written with the invariant culture.</summary>
  /// <param name="value">The text.</param>
  /// <param name="number">The integer.</param>
  /// <returns><see langword="true" /> when the text is an integer.</returns>
  private static bool TryParseNumber(string value, out int number)
  {
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
  }

  /// <summary>Tells whether a text is an absolute <c>http</c> or <c>https</c> URL.</summary>
  /// <param name="value">The text.</param>
  /// <returns><see langword="true" /> for an absolute <c>http</c> or <c>https</c> URL.</returns>
  private static bool IsHttpUrl(string value)
  {
    return Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
  }

  #endregion
}
