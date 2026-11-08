// ------------------------------------------------------------------------------------------------
// <copyright file="SettingDefinition.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>
///   A setting kept in the settings store: its configuration key, how it is shown and what values it
///   accepts.
/// </summary>
/// <remarks>
///   <para>
///     A definition is immutable. Every definition of the instance is declared once, in
///     <see cref="SettingDefinitions" />.
///   </para>
///   <para>
///     A secret never has a default: its value is protected by Data Protection before it is stored,
///     and it is never shown again.
///   </para>
/// </remarks>
public sealed class SettingDefinition
{
  #region Fields

  /// <summary>The separator of the sections of a configuration key.</summary>
  private const char SectionSeparator = ':';

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SettingDefinition" /> class.</summary>
  /// <param name="key">The configuration key.</param>
  /// <param name="group">The group under which the setting is shown.</param>
  /// <param name="type">The kind of value.</param>
  /// <param name="defaultValue">The value that applies when no source provides one.</param>
  /// <param name="requiresRestart">Whether a change only applies after the hosts restart.</param>
  /// <param name="min">The smallest accepted value of a number.</param>
  /// <param name="max">The largest accepted value of a number.</param>
  /// <param name="isSecret">Whether the setting is a secret.</param>
  private SettingDefinition(
    string key,
    string group,
    SettingType type,
    string? defaultValue,
    bool requiresRestart,
    int? min,
    int? max,
    bool isSecret)
  {
    Key = key;
    Group = group;
    Type = type;
    Default = defaultValue;
    RequiresRestart = requiresRestart;
    Min = min;
    Max = max;
    IsSecret = isSecret;
  }

  #endregion

  #region Properties

  /// <summary>
  ///   Gets the configuration key, as <c>Section:Name</c> (for example <c>GitHub:AppId</c>).
  /// </summary>
  public string Key { get; }

  /// <summary>Gets the group under which the setting is shown (for example <c>GitHub</c>).</summary>
  public string Group { get; }

  /// <summary>Gets the kind of value.</summary>
  public SettingType Type { get; }

  /// <summary>
  ///   Gets the value that applies when no source provides one, written with the invariant culture,
  ///   or <see langword="null" /> when there is none.
  /// </summary>
  public string? Default { get; }

  /// <summary>
  ///   Gets a value indicating whether a change only applies after the hosts restart; otherwise the
  ///   change applies at once.
  /// </summary>
  public bool RequiresRestart { get; }

  /// <summary>
  ///   Gets the smallest accepted value of a <see cref="SettingType.Number" /> setting, or
  ///   <see langword="null" /> when there is no lower bound.
  /// </summary>
  public int? Min { get; }

  /// <summary>
  ///   Gets the largest accepted value of a <see cref="SettingType.Number" /> setting, or
  ///   <see langword="null" /> when there is no upper bound.
  /// </summary>
  public int? Max { get; }

  /// <summary>
  ///   Gets a value indicating whether the setting is a secret, stored protected and never shown
  ///   again.
  /// </summary>
  public bool IsSecret { get; }

  #endregion

  #region Methods

  /// <summary>Creates a definition.</summary>
  /// <param name="key">The configuration key, as <c>Section:Name</c>.</param>
  /// <param name="group">The group under which the setting is shown.</param>
  /// <param name="type">The kind of value.</param>
  /// <param name="defaultValue">
  ///   The value that applies when no source provides one, or <see langword="null" /> for none.
  /// </param>
  /// <param name="requiresRestart">Whether a change only applies after the hosts restart.</param>
  /// <param name="min">The smallest accepted value of a number, or <see langword="null" />.</param>
  /// <param name="max">The largest accepted value of a number, or <see langword="null" />.</param>
  /// <param name="isSecret">Whether the setting is a secret.</param>
  /// <returns>The definition.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="key" /> or <paramref name="group" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="key" /> or <paramref name="group" /> is empty or white space,
  ///   <paramref name="key" /> has no section, bounds are given to a setting that is not a number or
  ///   are reversed, or a secret has a default or is not text.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="type" /> is not defined.</exception>
  public static SettingDefinition Create(
    string key,
    string group,
    SettingType type,
    string? defaultValue = null,
    bool requiresRestart = false,
    int? min = null,
    int? max = null,
    bool isSecret = false)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(key);
    ArgumentException.ThrowIfNullOrWhiteSpace(group);

    if (!Enum.IsDefined(type))
    {
      throw new ArgumentOutOfRangeException(
        nameof(type),
        type,
        "The type of the setting is not defined."
      );
    }

    var separator = key.IndexOf(SectionSeparator, StringComparison.Ordinal);

    if (separator <= 0 || separator == key.Length - 1)
    {
      throw new ArgumentException($"The key '{key}' must be written as 'Section:Name'.", nameof(key));
    }

    if ((min is not null || max is not null) && type != SettingType.Number)
    {
      throw new ArgumentException($"Only a number can be bounded: '{key}' is {type}.", nameof(type));
    }

    if (min > max)
    {
      throw new ArgumentException($"The bounds of '{key}' are reversed.", nameof(min));
    }

    if (isSecret && type != SettingType.Text)
    {
      throw new ArgumentException($"The secret '{key}' must be text.", nameof(type));
    }

    if (isSecret && defaultValue is not null)
    {
      throw new ArgumentException($"The secret '{key}' cannot have a default.", nameof(defaultValue));
    }

    return new SettingDefinition(key, group, type, defaultValue, requiresRestart, min, max, isSecret);
  }

  /// <inheritdoc />
  public override string ToString()
  {
    return Key;
  }

  #endregion
}
