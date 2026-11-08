// ------------------------------------------------------------------------------------------------
// <copyright file="JobClaimDialects.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql.Jobs;
using CodeCoverage.Infrastructure.SqlServer.Jobs;

namespace CodeCoverage.Infrastructure.Tests.Support;

// The job claim dialect of each engine, as the migrations assemblies register it.
internal static class JobClaimDialects
{
  #region Methods

  public static IJobClaimDialect For(DatabaseProvider provider) =>
    provider switch
    {
      DatabaseProvider.PostgreSql => new PostgreSqlJobClaimDialect(),
      DatabaseProvider.SqlServer => new SqlServerJobClaimDialect(),
      var _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider."),
    };

  public static IJobClaimDialect For(CodeCoverageDbContext context) =>
    For(context.Database.IsSqlServer() ? DatabaseProvider.SqlServer : DatabaseProvider.PostgreSql);

  #endregion
}
