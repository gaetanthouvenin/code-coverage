// ------------------------------------------------------------------------------------------------
// <copyright file="StoredSetting.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>A value saved in the settings store: one row of the <c>Settings</c> table.</summary>
/// <remarks>
///   <para>
///     A technical record of the Infrastructure, never seen by the Application: the use cases read
///     the settings through <c>ISettingsStore</c>, as <c>SettingValue</c>.
///   </para>
///   <para>
///     The value of a secret is its form protected by Data Protection, never the plain text: it is
///     what the running configuration exposes.
///   </para>
/// </remarks>
internal sealed class StoredSetting
{
  #region Fields

  /// <summary>The largest length of a configuration key.</summary>
  public const int KeyMaxLength = 128;

  #endregion

  #region Properties

  /// <summary>Gets or sets the configuration key, as <c>Section:Name</c>: the primary key.</summary>
  public required string Key { get; set; }

  /// <summary>
  ///   Gets or sets the value, written with the invariant culture; the protected form for a secret.
  ///   A <see langword="null" /> value is never exposed to the configuration.
  /// </summary>
  public string? Value { get; set; }

  /// <summary>Gets or sets a value indicating whether the value is a protected secret.</summary>
  public bool IsSecret { get; set; }

  /// <summary>Gets or sets when the value was last changed, in UTC.</summary>
  public DateTimeOffset UpdatedAt { get; set; }

  /// <summary>Gets or sets who last changed the value (a login, or the system).</summary>
  public required string UpdatedBy { get; set; }

  #endregion
}
