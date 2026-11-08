// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsChangeWatcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>
///   Brings into this host the settings saved by another host: polls the revision of the settings store
///   and refreshes the configuration when it moved, so that the <c>IOptionsMonitor</c> consumers are
///   notified.
/// </summary>
/// <remarks>
///   <para>
///     Portable to both engines, without LISTEN/NOTIFY (ADR-006): every save increments
///     <c>SettingsRevision.Revision</c> in its transaction, and each host reads it every
///     <see cref="SettingsWatcherOptions.PollSeconds" /> (5 s by default) on a <see cref="PeriodicTimer" />.
///     When the revision differs from the one the settings were loaded at
///     (<see cref="DatabaseSettingsConfigurationProvider.Revision" />), it calls
///     <see cref="DatabaseSettingsConfigurationProvider.RefreshAll" />.
///   </para>
///   <para>
///     The host that saves refreshes itself at once (<see cref="ConfigurationSettingsStore" />): its
///     loaded revision is then the current one and the watcher does not reload it a second time. A host
///     that started while the database was unavailable has no loaded revision and catches up on the first
///     successful poll.
///   </para>
///   <para>
///     A failed poll (database unreachable, timeout) is logged and retried on the next tick: the watcher
///     never stops the host, and the settings of the last successful load stay in place. A host without a
///     settings store (no connection string) has nothing to watch and the service ends at once.
///   </para>
/// </remarks>
internal sealed partial class SettingsChangeWatcher : BackgroundService
{
  #region Fields

  private readonly IConfigurationRoot? _configuration;

  private readonly ILogger<SettingsChangeWatcher> _logger;

  private readonly IOptionsMonitor<SettingsWatcherOptions> _options;

  private readonly TimeProvider _timeProvider;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SettingsChangeWatcher" /> class.</summary>
  /// <param name="configuration">The configuration of the host, whose settings store sources are refreshed.</param>
  /// <param name="options">The polling options, followed when they change.</param>
  /// <param name="timeProvider">The time provider of the polling timer.</param>
  /// <param name="logger">The logger.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public SettingsChangeWatcher(
    IConfiguration configuration,
    IOptionsMonitor<SettingsWatcherOptions> options,
    TimeProvider timeProvider,
    ILogger<SettingsChangeWatcher> logger)
  {
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(timeProvider);
    ArgumentNullException.ThrowIfNull(logger);

    _configuration = configuration as IConfigurationRoot;
    _options = options;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Methods

  /// <summary>Reads the revision of the store once and refreshes the configuration when it moved.</summary>
  /// <param name="cancellationToken">A token to cancel the poll.</param>
  /// <returns>
  ///   <see langword="true" /> when the configuration was refreshed; <see langword="false" /> when the revision
  ///   is unchanged, the schema does not exist yet, the poll failed or the host has no settings store.
  /// </returns>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
  internal async Task<bool> PollAsync(CancellationToken cancellationToken)
  {
    var stores = Stores();

    if (stores.Count == 0)
    {
      return false;
    }

    try
    {
      var current = await stores[0].ReadRevisionAsync(cancellationToken).ConfigureAwait(false);

      // No schema yet (the Api has not migrated), or nothing saved since the last load.
      if (current is null || stores.TrueForAll(s => s.Revision == current))
      {
        return false;
      }

      LogRefreshing(_logger, stores[0].Revision, current.Value);
      DatabaseSettingsConfigurationProvider.RefreshAll(_configuration!);

      return true;
    }
    catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
      LogPollFailed(_logger, exception);

      return false;
    }
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (Stores().Count == 0)
    {
      LogNoStore(_logger);
      return;
    }

    var options = _options.CurrentValue;
    using var timer = new PeriodicTimer(options.PollInterval, _timeProvider);
    using var subscription = _options.OnChange(o => ApplyPeriod(timer, o));

    LogWatching(_logger, options.PollSeconds);

    try
    {
      // A first poll at once: a host that started while the database was unavailable catches up early.
      do
      {
        await PollAsync(stoppingToken).ConfigureAwait(false);
      }
      while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // The host is stopping.
    }
  }

  /// <summary>Applies a new polling period to the running timer, when it is within its bounds.</summary>
  /// <param name="timer">The polling timer.</param>
  /// <param name="options">The new options.</param>
  private void ApplyPeriod(PeriodicTimer timer, SettingsWatcherOptions options)
  {
    if (!options.IsValid())
    {
      LogInvalidPeriod(_logger, options.PollSeconds);
      return;
    }

    try
    {
      timer.Period = options.PollInterval;
    }
    catch (ObjectDisposedException)
    {
      // The watcher stopped while the configuration changed.
    }
  }

  /// <summary>Lists the settings store sources of the configuration.</summary>
  /// <returns>The sources; empty when the host has no settings store.</returns>
  private List<DatabaseSettingsConfigurationProvider> Stores() =>
    _configuration?.Providers.OfType<DatabaseSettingsConfigurationProvider>().ToList() ?? [];

  [LoggerMessage(
    EventId = 1100,
    Level = LogLevel.Information,
    Message = "No settings store in the configuration: the settings change watcher is idle"
  )]
  private static partial void LogNoStore(ILogger logger);

  [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Watching the settings revision every {PollSeconds} s")]
  private static partial void LogWatching(ILogger logger, int pollSeconds);

  [LoggerMessage(
    EventId = 1102,
    Level = LogLevel.Information,
    Message = "Settings revision moved from {Previous} to {Current}: refreshing the configuration"
  )]
  private static partial void LogRefreshing(ILogger logger, long? previous, long current);

  [LoggerMessage(
    EventId = 1103,
    Level = LogLevel.Warning,
    Message = "Settings revision poll failed; the current settings stay in place until the next poll"
  )]
  private static partial void LogPollFailed(ILogger logger, Exception exception);

  [LoggerMessage(
    EventId = 1104,
    Level = LogLevel.Warning,
    Message = "Settings:PollSeconds {PollSeconds} is out of bounds: the polling period is left unchanged"
  )]
  private static partial void LogInvalidPeriod(ILogger logger, int pollSeconds);

  #endregion
}
