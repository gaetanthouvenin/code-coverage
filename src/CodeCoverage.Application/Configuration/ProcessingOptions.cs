// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Processing</c> settings group: the background processing.</summary>
/// <remarks>
///   Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart,
///   except for <see cref="WorkerConcurrency" />.
/// </remarks>
public sealed class ProcessingOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.ProcessingGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the number of jobs run at the same time by a worker.</summary>
  /// <value>4 by default; a change only applies after the hosts restart.</value>
  public int WorkerConcurrency { get; set; } = 4;

  /// <summary>Gets or sets the number of reports parsed at the same time.</summary>
  /// <value>2 by default.</value>
  public int MaxParallelParses { get; set; } = 2;

  /// <summary>Gets or sets the number of attempts of a job before it fails for good.</summary>
  /// <value>8 by default.</value>
  public int JobMaxAttempts { get; set; } = 8;

  /// <summary>Gets or sets the number of ancestors searched for a carried-forward flag.</summary>
  /// <value>10 by default.</value>
  public int CarryforwardMaxDepth { get; set; } = 10;

  /// <summary>Gets or sets the time waited for the parent of a carried-forward flag, in minutes.</summary>
  /// <value>10 by default.</value>
  public int CarryforwardWaitMinutes { get; set; } = 10;

  /// <summary>Gets or sets the number of ancestors walked to find the base of a comparison.</summary>
  /// <value>30 by default.</value>
  public int BaseMaxAncestorWalk { get; set; } = 30;

  /// <summary>Gets or sets the time waited for the base of a comparison, in minutes.</summary>
  /// <value>15 by default.</value>
  public int BaseWaitMinutes { get; set; } = 15;

  /// <summary>Gets or sets the time after which a pending commit status is closed, in minutes.</summary>
  /// <value>60 by default.</value>
  public int PendingTimeoutMinutes { get; set; } = 60;

  #endregion
}
