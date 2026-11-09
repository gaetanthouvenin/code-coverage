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

  /// <summary>The setup token is missing, wrong or already used.</summary>
  public const string InvalidSetupToken = "Setting.InvalidSetupToken";

  /// <summary>GitHub does not know the code of the manifest, or it has expired.</summary>
  public const string InvalidManifestCode = "Setting.InvalidManifestCode";

  /// <summary>The GitHub App already exists on this instance.</summary>
  public const string GitHubAppAlreadyCreated = "Setting.GitHubAppAlreadyCreated";

  /// <summary>The setup cannot end before the GitHub App exists.</summary>
  public const string GitHubAppNotCreated = "Setting.GitHubAppNotCreated";

  /// <summary>The list of administrators is empty or holds something other than numeric identifiers.</summary>
  public const string InvalidAdministrators = "Setting.InvalidAdministrators";

  #endregion
}
