// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Persistence;

/// <summary>The <c>CodeCoverage:Database</c> configuration section.</summary>
/// <remarks>
///   Bootstrap settings, read from the environment only (<c>CodeCoverage__Database__Provider</c>,
///   <c>CodeCoverage__Database__MigrateOnStartup</c>): the database must be known before the settings
///   stored in it can be read. The connection string is <c>ConnectionStrings:CodeCoverage</c>.
/// </remarks>
public sealed class DatabaseOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = "CodeCoverage:Database";

  /// <summary>The name of the connection string (<c>ConnectionStrings:CodeCoverage</c>).</summary>
  public const string ConnectionStringName = "CodeCoverage";

  #endregion

  #region Properties

  /// <summary>Gets or sets the database engine.</summary>
  /// <value>The database engine; <see cref="DatabaseProvider.PostgreSql" /> by default.</value>
  public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSql;

  /// <summary>Gets or sets a value indicating whether the pending migrations are applied at start-up.</summary>
  /// <value>
  ///   <see langword="true" /> (the default) to migrate the schema when the host starts; <see langword="false" />
  ///   when the schema is migrated elsewhere (another replica, a DBA script).
  /// </value>
  /// <remarks>
  ///   Only a host that registers the migrator (<c>AddDatabaseMigration</c>, the Api) reads it: the Web
  ///   never migrates and only reports the schema state through its readiness probe.
  /// </remarks>
  public bool MigrateOnStartup { get; set; } = true;

  #endregion
}
