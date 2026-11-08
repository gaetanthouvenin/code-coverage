// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.SqlServer.Jobs;

namespace CodeCoverage.Infrastructure.SqlServer;

/// <summary>Registers the SQL Server-specific services of this migrations assembly.</summary>
public static class DependencyInjection
{
  #region Methods

  /// <summary>
  ///   Registers the job claim dialect of SQL Server (<c>WITH (ROWLOCK, UPDLOCK, READPAST)</c>), keyed
  ///   by <see cref="DatabaseProvider.SqlServer" />.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The hosts call it next to <c>AddInfrastructure()</c>, with the method of every other engine: the
  ///   engine is chosen at run time by <c>CodeCoverage:Database:Provider</c>, and
  ///   <c>AddInfrastructure()</c> picks the dialect registered under its key. Calling it twice is
  ///   harmless.
  /// </remarks>
  public static IServiceCollection AddSqlServerProvider(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddKeyedSingleton<IJobClaimDialect, SqlServerJobClaimDialect>(DatabaseProvider.SqlServer);

    return services;
  }

  #endregion
}
