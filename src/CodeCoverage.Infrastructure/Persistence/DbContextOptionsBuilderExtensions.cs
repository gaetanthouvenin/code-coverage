// ------------------------------------------------------------------------------------------------
// <copyright file="DbContextOptionsBuilderExtensions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

namespace CodeCoverage.Infrastructure.Persistence;

/// <summary>Points a <see cref="CodeCoverageDbContext" /> at the chosen database engine.</summary>
public static class DbContextOptionsBuilderExtensions
{
  #region Fields

  /// <summary>The name of the assembly holding the PostgreSQL migrations.</summary>
  public const string PostgreSqlMigrationsAssembly = "CodeCoverage.Infrastructure.PostgreSql";

  /// <summary>The name of the assembly holding the SQL Server migrations.</summary>
  public const string SqlServerMigrationsAssembly = "CodeCoverage.Infrastructure.SqlServer";

  #endregion

  #region Methods

  /// <summary>Configures the options for the database engine and its migrations assembly.</summary>
  /// <param name="options">The options builder.</param>
  /// <param name="provider">The database engine.</param>
  /// <param name="connectionString">The connection string.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="connectionString" /> is <see langword="null" />, empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="provider" /> is not a known engine.</exception>
  /// <remarks>
  ///   Each engine keeps its own migrations in its own assembly, since the generated SQL differs. The
  ///   migrations assemblies reference this one, so they are named here, not referenced; the hosts
  ///   deploy them next to the executable.
  /// </remarks>
  public static DbContextOptionsBuilder UseCodeCoverageDatabase(
    this DbContextOptionsBuilder options,
    DatabaseProvider provider,
    string connectionString)
  {
    ArgumentNullException.ThrowIfNull(options);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    _ = provider switch
    {
      DatabaseProvider.PostgreSql => options.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsAssembly(PostgreSqlMigrationsAssembly)
      ),
      DatabaseProvider.SqlServer => options.UseSqlServer(
        connectionString,
        sql => sql.MigrationsAssembly(SqlServerMigrationsAssembly)
      ),
      var _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider."),
    };

    return options;
  }

  #endregion
}
