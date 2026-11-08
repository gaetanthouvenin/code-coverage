// ------------------------------------------------------------------------------------------------
// <copyright file="SettingErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Settings;

/// <summary>The error codes of the settings.</summary>
public static class SettingErrors
{
  #region Fields

  /// <summary>The key is not in the catalog of the settings.</summary>
  public const string Unknown = "Setting.Unknown";

  /// <summary>
  ///   A stronger source (command line, environment variable or <c>appsettings.json</c>) provides the
  ///   value: it is read-only on screen and through the API.
  /// </summary>
  public const string ProvidedByEnvironment = "Setting.ProvidedByEnvironment";

  /// <summary>
  ///   The number is below the minimum or above the maximum of the setting, or the text is longer than
  ///   its limit.
  /// </summary>
  public const string OutOfRange = "Setting.OutOfRange";

  /// <summary>The setting is not a secret: its value is changed, not replaced.</summary>
  public const string NotSecret = "Setting.NotSecret";

  /// <summary>The setting is a secret: its value is replaced, never changed in clear.</summary>
  public const string IsSecret = "Setting.IsSecret";

  /// <summary>The value does not match the type of the setting (number, boolean or URL).</summary>
  public const string InvalidValue = "Setting.InvalidValue";

  #endregion
}
