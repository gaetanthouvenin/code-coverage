// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Persistence;

/// <summary>The database engine the CodeCoverage data is stored in.</summary>
/// <remarks>
///   One EF Core model serves both engines; each one has its own migrations assembly
///   (<c>Infrastructure.PostgreSql</c>, <c>Infrastructure.SqlServer</c>).
/// </remarks>
public enum DatabaseProvider
{
  /// <summary>PostgreSQL: the default, for the self-hosted deployment (<c>docker-compose.yml</c>).</summary>
  PostgreSql = 0,

  /// <summary>SQL Server, with <c>READ_COMMITTED_SNAPSHOT ON</c>.</summary>
  SqlServer = 1,
}
