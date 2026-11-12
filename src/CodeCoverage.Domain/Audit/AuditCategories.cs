// ------------------------------------------------------------------------------------------------
// <copyright file="AuditCategories.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Audit;

/// <summary>Tells the family of an audit action: one rule for the filter of the reader and the label of the page.</summary>
public static class AuditCategories
{
  #region Fields

  /// <summary>The action of a secret replaced.</summary>
  public const string ReplaceSecretAction = "Settings.ReplaceSecret";

  /// <summary>The action of a setting reset, a secret cleared when its key is one of a secret.</summary>
  public const string ResetSettingAction = "Settings.Reset";

  /// <summary>The prefix of the changes of the settings of the instance.</summary>
  public const string SettingsPrefix = "Settings.";

  /// <summary>The prefix of the lifecycle of the organizations.</summary>
  public const string OrganizationsPrefix = "Organization.";

  /// <summary>The prefix of the API tokens.</summary>
  public const string TokensPrefix = "ApiTokens.";

  /// <summary>The action of a job retried.</summary>
  public const string RetryJobAction = "Jobs.Retry";

  #endregion

  #region Properties

  /// <summary>Gets the keys of the secret settings: their reset is the clearing of a secret.</summary>
  public static IReadOnlySet<string> SecretKeys { get; } =
    SettingDefinitions.All.Where(static d => d.IsSecret).Select(static d => d.Key).ToFrozenSet(StringComparer.Ordinal);

  #endregion

  #region Methods

  /// <summary>Tells the family of an action.</summary>
  /// <param name="action">The action.</param>
  /// <param name="target">What it was done to.</param>
  /// <returns>The family, or <see langword="null" /> for an action no family holds (shown under « All » only).</returns>
  /// <exception cref="ArgumentNullException"><paramref name="action" /> or <paramref name="target" /> is <see langword="null" />.</exception>
  public static AuditCategory? Of(string action, string target)
  {
    ArgumentNullException.ThrowIfNull(action);
    ArgumentNullException.ThrowIfNull(target);

    return action switch
    {
      ReplaceSecretAction => AuditCategory.Secrets,
      ResetSettingAction when SecretKeys.Contains(target) => AuditCategory.Secrets,
      RetryJobAction => AuditCategory.Retries,
      var _ when action.StartsWith(SettingsPrefix, StringComparison.Ordinal) => AuditCategory.Settings,
      var _ when action.StartsWith(OrganizationsPrefix, StringComparison.Ordinal) => AuditCategory.Organizations,
      var _ when action.StartsWith(TokensPrefix, StringComparison.Ordinal) => AuditCategory.Tokens,
      var _ => null,
    };
  }

  #endregion
}
