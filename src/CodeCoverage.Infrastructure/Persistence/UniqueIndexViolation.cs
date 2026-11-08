// ------------------------------------------------------------------------------------------------
// <copyright file="UniqueIndexViolation.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace CodeCoverage.Infrastructure.Persistence;

/// <summary>Recognizes, on both engines, a save refused by a given unique index.</summary>
/// <remarks>
///   <para>
///     The index is checked by name, so that the violation of another constraint (a primary key, a
///     foreign key) is never mistaken for it: PostgreSQL reports the name of the constraint, SQL Server
///     quotes it in the message of errors 2601 and 2627.
///   </para>
///   <para>
///     A save of the change tracker reports the error of the engine wrapped in a
///     <see cref="DbUpdateException" />; a raw statement (<c>FromSql</c>, <c>ExecuteUpdate</c>) reports
///     it directly: both are recognized.
///   </para>
/// </remarks>
internal static class UniqueIndexViolation
{
  #region Fields

  /// <summary>The SQL Server error of a duplicate key in a unique index.</summary>
  private const int SqlServerDuplicateKeyInIndex = 2601;

  /// <summary>The SQL Server error of a duplicate key in a unique constraint.</summary>
  private const int SqlServerDuplicateKeyInConstraint = 2627;

  #endregion

  #region Methods

  /// <summary>Tells whether a save was refused by a unique index.</summary>
  /// <param name="exception">
  ///   The failure of the save: a <see cref="DbUpdateException" /> wrapping the error of the engine, or
  ///   the <see cref="DbException" /> of the engine itself.
  /// </param>
  /// <param name="indexName">The name of the unique index.</param>
  /// <returns>
  ///   <see langword="true" /> when the engine refused the save because of <paramref name="indexName" />;
  ///   otherwise <see langword="false" />.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="exception" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="indexName" /> is empty or white space.</exception>
  public static bool Is(Exception exception, string indexName)
  {
    ArgumentNullException.ThrowIfNull(exception);
    ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

    var engineError = exception as DbException ?? exception.InnerException;

    return engineError switch
    {
      PostgresException postgres => postgres.SqlState == PostgresErrorCodes.UniqueViolation
                                    && string.Equals(postgres.ConstraintName, indexName, StringComparison.Ordinal),
      SqlException sql => sql.Number is SqlServerDuplicateKeyInIndex or SqlServerDuplicateKeyInConstraint
                          && sql.Message.Contains($"'{indexName}'", StringComparison.Ordinal),
      var _ => false,
    };
  }

  #endregion
}
