// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseMigrator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCoverage.Infrastructure.Persistence;

/// <summary>Brings the database schema up to date when the host starts.</summary>
/// <param name="scopeFactory">Creates the scope of the migration context.</param>
/// <param name="options">The database options (<see cref="DatabaseOptions.MigrateOnStartup" />).</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     A hosted service registered before the web server: the host does not answer until the schema
///     is up to date, and a failed migration stops the start-up.
///   </para>
///   <para>
///     The migration lock of EF Core keeps two hosts starting together from migrating at the same
///     time. Only the Api registers it (<see cref="DependencyInjection.AddDatabaseMigration" />); the
///     Web stays not ready (<c>/health/ready</c>) until the Api has migrated.
///   </para>
/// </remarks>
internal sealed partial class DatabaseMigrator(
  IServiceScopeFactory scopeFactory,
  IOptions<DatabaseOptions> options,
  ILogger<DatabaseMigrator> logger) : IHostedService
{
  #region Methods

  [LoggerMessage(
    EventId = 1000,
    Level = LogLevel.Information,
    Message = "Database migration skipped (CodeCoverage:Database:MigrateOnStartup is off)"
  )]
  private static partial void LogMigrationSkipped(ILogger logger);

  [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "{Provider} database schema is up to date")]
  private static partial void LogUpToDate(ILogger logger, DatabaseProvider provider);

  [LoggerMessage(
    EventId = 1002,
    Level = LogLevel.Information,
    Message = "Applied {Count} migration(s) to the {Provider} database: {Migrations}"
  )]
  private static partial void LogMigrated(ILogger logger, int count, DatabaseProvider provider, string migrations);

  #endregion

  #region Implementation of IHostedService

  /// <inheritdoc />
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    var database = options.Value;

    if (!database.MigrateOnStartup)
    {
      LogMigrationSkipped(logger);
      return;
    }

    var scope = scopeFactory.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var context = scope.ServiceProvider.GetRequiredService<CodeCoverageDbContext>();
      var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

      if (pending.Count == 0)
      {
        LogUpToDate(logger, database.Provider);
        return;
      }

      await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
      LogMigrated(logger, pending.Count, database.Provider, string.Join(", ", pending));
    }
  }

  /// <inheritdoc />
  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  #endregion
}
