// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsWatcherOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>The <c>Settings</c> configuration section: how often the <see cref="SettingsChangeWatcher" /> polls.</summary>
/// <remarks>
///   A bootstrap setting, read from the environment (<c>Settings__PollSeconds</c>, ARCHITECTURE §4bis.2):
///   it drives the propagation of the settings store itself, so it is not part of the store catalog. A
///   change of the configuration still applies to the running timer.
/// </remarks>
public sealed class SettingsWatcherOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = "Settings";

  /// <summary>The default polling period, in seconds: a change is seen by the other host in 5 s at most.</summary>
  public const int DefaultPollSeconds = 5;

  /// <summary>The shortest polling period, in seconds.</summary>
  public const int MinPollSeconds = 1;

  /// <summary>The longest polling period, in seconds.</summary>
  public const int MaxPollSeconds = 300;

  #endregion

  #region Properties

  /// <summary>Gets or sets the period between two reads of the settings revision, in seconds.</summary>
  /// <value>
  ///   Between <see cref="MinPollSeconds" /> and <see cref="MaxPollSeconds" />; <see cref="DefaultPollSeconds" />
  ///   by default.
  /// </value>
  public int PollSeconds { get; set; } = DefaultPollSeconds;

  /// <summary>Gets the polling period.</summary>
  /// <value>The period of <see cref="PollSeconds" />.</value>
  public TimeSpan PollInterval => TimeSpan.FromSeconds(PollSeconds);

  #endregion

  #region Methods

  /// <summary>Tells whether the options are within their bounds.</summary>
  /// <returns><see langword="true" /> when <see cref="PollSeconds" /> is within its bounds.</returns>
  public bool IsValid() => PollSeconds is >= MinPollSeconds and <= MaxPollSeconds;

  #endregion
}
