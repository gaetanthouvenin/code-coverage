// ------------------------------------------------------------------------------------------------
// <copyright file="RetentionOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Retention</c> settings group: the data retention.</summary>
/// <remarks>Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart.</remarks>
public sealed class RetentionOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.RetentionGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the number of days the raw uploaded reports are kept.</summary>
  /// <value>30 by default.</value>
  public int RawDays { get; set; } = 30;

  /// <summary>Gets or sets the number of days the reports of a deleted branch are kept.</summary>
  /// <value>180 by default.</value>
  public int BranchReportDays { get; set; } = 180;

  /// <summary>Gets or sets the number of days the finished jobs are kept.</summary>
  /// <value>14 by default.</value>
  public int JobDays { get; set; } = 14;

  /// <summary>Gets or sets the number of days the received webhooks are kept.</summary>
  /// <value>14 by default.</value>
  public int WebhookDays { get; set; } = 14;

  /// <summary>Gets or sets the number of days the cached diffs are kept.</summary>
  /// <value>30 by default.</value>
  public int DiffCacheDays { get; set; } = 30;

  #endregion
}
