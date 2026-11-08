// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Notifications</c> settings group: the commit statuses and pull request comments.</summary>
/// <remarks>Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart.</remarks>
public sealed class NotificationOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.NotificationsGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the prefix of the commit status contexts.</summary>
  /// <value><c>coverage</c> by default.</value>
  public string StatusContextPrefix { get; set; } = "coverage";

  /// <summary>Gets or sets a value indicating whether the commit statuses are sent.</summary>
  /// <value><see langword="true" /> by default.</value>
  public bool StatusesEnabled { get; set; } = true;

  /// <summary>Gets or sets a value indicating whether the pull request comments are sent.</summary>
  /// <value><see langword="true" /> by default.</value>
  public bool CommentsEnabled { get; set; } = true;

  /// <summary>Gets or sets the time waited for further uploads before notifying, in seconds.</summary>
  /// <value>20 by default.</value>
  public int DebounceSeconds { get; set; } = 20;

  /// <summary>Gets or sets the longest delay of a notification by the debounce, in seconds.</summary>
  /// <value>300 by default.</value>
  public int MaxDelaySeconds { get; set; } = 300;

  /// <summary>
  ///   Gets or sets a value indicating whether the carried-forward sessions count in <c>after_n_builds</c>.
  /// </summary>
  /// <value><see langword="true" /> by default.</value>
  public bool AfterNBuildsCountsCarryforward { get; set; } = true;

  /// <summary>Gets or sets the title of the pull request comment.</summary>
  /// <value><c>Coverage</c> by default.</value>
  public string CommentTitle { get; set; } = "Coverage";

  #endregion
}
