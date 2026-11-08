// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseSettingsConfigurationProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;
using System.Globalization;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

using Npgsql;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>
///   Feeds the settings saved from the screen into the configuration, so that the hosts read them like
///   any other setting (<c>IConfiguration</c>, <c>IOptionsMonitor</c>). Added as the weakest source:
///   the command line, the environment variables and <c>appsettings.json</c> win over it.
/// </summary>
/// <param name="connectionString">The connection string of the database.</param>
/// <param name="provider">The database engine.</param>
/// <remarks>
///   <para>
///     Read in plain ADO.NET (<see cref="NpgsqlConnection" /> or <see cref="SqlConnection" />) rather
///     than EF Core: the configuration is built before the services.
///   </para>
///   <para>
///     A missing database or table (first start, before the Api migrates) yields no setting, never an
///     error that would stop the host. Any other database failure (server unreachable, timeout) keeps
///     the settings of the last successful load, empty on the first one: a refresh that fails never
///     brings the defaults back, and the readiness probe reports the database.
///   </para>
///   <para>
///     The value of a secret is exposed as stored, that is in its protected form only.
///   </para>
/// </remarks>
public sealed class DatabaseSettingsConfigurationProvider(string connectionString, DatabaseProvider provider)
  : ConfigurationProvider
{
  #region Fields

  /// <summary>The name of the table of the settings store.</summary>
  public const string TableName = "Settings";

  /// <summary>The SQL Server error of an unknown object (the table does not exist).</summary>
  private const int SqlServerInvalidObjectName = 208;

  /// <summary>The SQL Server error of a database that cannot be opened (it does not exist).</summary>
  private const int SqlServerCannotOpenDatabase = 4060;

  /// <summary>The query of PostgreSQL, with its quoted identifiers.</summary>
  private const string PostgreSqlQuery = "SELECT \"Key\", \"Value\" FROM \"" + TableName + "\"";

  /// <summary>The query of SQL Server, with its bracketed identifiers.</summary>
  private const string SqlServerQuery = "SELECT [Key], [Value] FROM [" + TableName + "]";

  /// <summary>The name of the single-row table of the settings revision.</summary>
  private const string RevisionTableName = "SettingsRevision";

  /// <summary>The revision query of PostgreSQL, with its quoted identifiers.</summary>
  private const string PostgreSqlRevisionQuery =
    "SELECT \"Revision\" FROM \"" + RevisionTableName + "\" WHERE \"Id\" = 1";

  /// <summary>The revision query of SQL Server, with its bracketed identifiers.</summary>
  private const string SqlServerRevisionQuery = "SELECT [Revision] FROM [" + RevisionTableName + "] WHERE [Id] = 1";

  #endregion

  #region Properties

  /// <summary>Gets the revision of the store that the current settings were loaded at.</summary>
  /// <value>
  ///   The value of <c>SettingsRevision.Revision</c> read by the last successful load; <see langword="null" />
  ///   before the first one, or while the database or its tables do not exist.
  /// </value>
  /// <remarks>
  ///   Read before the settings, in the same connection: a save committed in between yields newer
  ///   settings with an older revision, which only costs the <see cref="SettingsChangeWatcher" /> one
  ///   more refresh, never a missed change.
  /// </remarks>
  internal long? Revision { get; private set; }

  #endregion

  #region Methods

  /// <summary>Reloads every settings store source of a configuration, for example after a setting was saved.</summary>
  /// <param name="root">The configuration root.</param>
  /// <exception cref="ArgumentNullException"><paramref name="root" /> is <see langword="null" />.</exception>
  /// <remarks>The <c>IOptionsMonitor</c> consumers are notified through the reload token of the configuration.</remarks>
  public static void RefreshAll(IConfigurationRoot root)
  {
    ArgumentNullException.ThrowIfNull(root);

    foreach (var store in root.Providers.OfType<DatabaseSettingsConfigurationProvider>())
    {
      store.Refresh();
    }
  }

  /// <summary>Reloads the settings from the store and notifies the configuration of the change.</summary>
  public void Refresh()
  {
    Load();
    OnReload();
  }

  /// <inheritdoc />
  public override void Load()
  {
    try
    {
      (Data, Revision) = ReadSettings();
    }
    catch (DbException exception) when (IsMissingSchema(exception))
    {
      // First start: the Api has not migrated yet; the watcher refreshes this source afterwards.
      ForgetMissingDatabase(exception);
      Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
      Revision = null;
    }
    catch (DbException)
    {
      // The database is unavailable: the settings of the last successful load stay in place.
    }
  }

  /// <summary>Reads the current revision of the store, without loading the settings.</summary>
  /// <param name="cancellationToken">A token to cancel the query.</param>
  /// <returns>
  ///   The value of <c>SettingsRevision.Revision</c>; <see langword="null" /> while the database, its table
  ///   or its row do not exist (the Api has not migrated yet).
  /// </returns>
  /// <exception cref="DbException">The database is unavailable (server unreachable, timeout).</exception>
  /// <remarks>Polled by the <see cref="SettingsChangeWatcher" />: a single-row query, cheap on both engines.</remarks>
  internal async Task<long?> ReadRevisionAsync(CancellationToken cancellationToken)
  {
    var (connection, _, revisionQuery) = Connect();

    await using (connection.ConfigureAwait(false))
    {
      try
      {
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();

        await using (command.ConfigureAwait(false))
        {
          command.CommandText = revisionQuery;

          var revision = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

          return revision is null or DBNull ? null : Convert.ToInt64(revision, CultureInfo.InvariantCulture);
        }
      }
      catch (DbException exception) when (IsMissingSchema(exception))
      {
        ForgetMissingDatabase(exception);

        return null;
      }
    }
  }

  /// <summary>Clears the SQL Server connection pool after a database that could not be opened.</summary>
  /// <param name="exception">The failure.</param>
  /// <remarks>
  ///   Outside Azure, <see cref="SqlConnection" /> blocks a pool whose login failed and replays the same
  ///   error for up to five seconds, then longer on each new failure. Without the clearing, the read that
  ///   follows the migration of the Api would still report the database as missing.
  /// </remarks>
  private void ForgetMissingDatabase(DbException exception)
  {
    if (exception is SqlException { Number: SqlServerCannotOpenDatabase })
    {
      using var connection = new SqlConnection(connectionString);
      SqlConnection.ClearPool(connection);
    }
  }

  /// <summary>Tells whether a failure means that the database or the table does not exist yet.</summary>
  /// <param name="exception">The failure.</param>
  /// <returns><see langword="true" /> for a missing database or table.</returns>
  private static bool IsMissingSchema(DbException exception) =>
    exception switch
    {
      PostgresException { SqlState: PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidCatalogName } => true,
      SqlException { Number: SqlServerInvalidObjectName or SqlServerCannotOpenDatabase } => true,
      var _ => false,
    };

  /// <summary>Opens a connection to the configured engine.</summary>
  /// <returns>The connection, not opened yet, and the settings and revision queries of the engine.</returns>
  /// <exception cref="InvalidOperationException">The engine is not known.</exception>
  private (DbConnection Connection, string Query, string RevisionQuery) Connect() =>
    provider switch
    {
      DatabaseProvider.PostgreSql => (new NpgsqlConnection(connectionString), PostgreSqlQuery, PostgreSqlRevisionQuery),
      DatabaseProvider.SqlServer => (new SqlConnection(connectionString), SqlServerQuery, SqlServerRevisionQuery),
      var _ => throw new InvalidOperationException($"Unknown database provider '{provider}'."),
    };

  /// <summary>Reads the revision, then every value of the store; a <see langword="null" /> value is left out.</summary>
  /// <returns>The values, by case-insensitive key, and the revision they were read at.</returns>
  private (Dictionary<string, string?> Data, long? Revision) ReadSettings()
  {
    var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var (connection, query, revisionQuery) = Connect();
    long? revision;

    using (connection)
    {
      connection.Open();

      using (var revisionCommand = connection.CreateCommand())
      {
        revisionCommand.CommandText = revisionQuery;

        var value = revisionCommand.ExecuteScalar();
        revision = value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
      }

      using var command = connection.CreateCommand();
      command.CommandText = query;

      using var reader = command.ExecuteReader();

      while (reader.Read())
      {
        if (!reader.IsDBNull(1))
        {
          data[reader.GetString(0)] = reader.GetString(1);
        }
      }
    }

    return (data, revision);
  }

  #endregion
}
