// ------------------------------------------------------------------------------------------------
// <copyright file="SettingValue.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>The effective value of a setting and where it comes from.</summary>
/// <remarks>
///   <para>
///     The value of a secret, when present, is its protected form, never the plain text. A value
///     meant to leave the application is first passed through <see cref="Redact" />, which drops it
///     and keeps only whether the secret is defined.
///   </para>
///   <para>
///     <see cref="UpdatedAt" /> and <see cref="UpdatedBy" /> describe the last change saved in the
///     settings store; they are kept when a stronger source overrides that value.
///   </para>
/// </remarks>
public sealed class SettingValue
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SettingValue" /> class.</summary>
  /// <param name="definition">The definition of the setting.</param>
  /// <param name="value">The effective value.</param>
  /// <param name="isDefined">Whether a value is defined.</param>
  /// <param name="source">The source of the effective value.</param>
  /// <param name="updatedAt">When the value of the store was last changed.</param>
  /// <param name="updatedBy">Who last changed the value of the store.</param>
  private SettingValue(
    SettingDefinition definition,
    string? value,
    bool isDefined,
    SettingSource source,
    DateTimeOffset? updatedAt,
    string? updatedBy)
  {
    Definition = definition;
    Value = value;
    IsDefined = isDefined;
    Source = source;
    UpdatedAt = updatedAt;
    UpdatedBy = updatedBy;
  }

  #endregion

  #region Properties

  /// <summary>Gets the definition of the setting.</summary>
  public SettingDefinition Definition { get; }

  /// <summary>Gets the configuration key of the setting.</summary>
  public string Key => Definition.Key;

  /// <summary>
  ///   Gets the effective value, written with the invariant culture, or <see langword="null" /> when
  ///   none is defined or when the value was removed by <see cref="Redact" />.
  /// </summary>
  public string? Value { get; }

  /// <summary>
  ///   Gets a value indicating whether a value is defined, by a source or by the default of the
  ///   catalog; still known once the value of a secret was removed by <see cref="Redact" />.
  /// </summary>
  public bool IsDefined { get; }

  /// <summary>Gets the source of the effective value.</summary>
  public SettingSource Source { get; }

  /// <summary>
  ///   Gets when the value of the store was last changed, in UTC, or <see langword="null" /> when
  ///   the store holds no value.
  /// </summary>
  public DateTimeOffset? UpdatedAt { get; }

  /// <summary>
  ///   Gets who last changed the value of the store, or <see langword="null" /> when the store holds
  ///   no value.
  /// </summary>
  public string? UpdatedBy { get; }

  /// <summary>
  ///   Gets a value indicating whether the value can be changed on screen: only a value from the
  ///   store, or the default, can be; a value from a stronger source is read-only.
  /// </summary>
  public bool IsEditable => Source != SettingSource.Environment;

  #endregion

  #region Methods

  /// <summary>Creates the effective value of a setting.</summary>
  /// <param name="definition">The definition of the setting.</param>
  /// <param name="value">
  ///   The effective value, or <see langword="null" /> when none is defined; the protected form for a
  ///   secret.
  /// </param>
  /// <param name="source">The source of the effective value.</param>
  /// <param name="updatedAt">
  ///   When the value of the store was last changed, or <see langword="null" />; converted to UTC.
  /// </param>
  /// <param name="updatedBy">Who last changed the value of the store, or <see langword="null" />.</param>
  /// <returns>The effective value.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="definition" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="value" /> is <see langword="null" /> while <paramref name="source" /> names a
  ///   source that provides one (<see cref="SettingSource.Store" /> or
  ///   <see cref="SettingSource.Environment" />).
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="source" /> is not defined.</exception>
  public static SettingValue Create(
    SettingDefinition definition,
    string? value,
    SettingSource source,
    DateTimeOffset? updatedAt,
    string? updatedBy)
  {
    ArgumentNullException.ThrowIfNull(definition);

    if (!Enum.IsDefined(source))
    {
      throw new ArgumentOutOfRangeException(
        nameof(source),
        source,
        "The source of the setting is not defined."
      );
    }

    if (value is null && source != SettingSource.Default)
    {
      throw new ArgumentException(
        $"The source {source} provides a value: the value of '{definition.Key}' cannot be null.",
        nameof(value)
      );
    }

    return new SettingValue(
      definition,
      value,
      value is not null,
      source,
      updatedAt?.ToUniversalTime(),
      updatedBy
    );
  }

  /// <summary>Returns a copy safe to show outside the application.</summary>
  /// <returns>
  ///   For a secret, a copy without its value that still tells whether it is defined; otherwise the
  ///   same instance.
  /// </returns>
  public SettingValue Redact()
  {
    return Definition.IsSecret && Value is not null
             ? new SettingValue(Definition, null, IsDefined, Source, UpdatedAt, UpdatedBy)
             : this;
  }

  #endregion
}
