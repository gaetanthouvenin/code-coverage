// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql.Jobs;

namespace CodeCoverage.Infrastructure.PostgreSql;

/// <summary>Registers the PostgreSQL-specific services of this migrations assembly.</summary>
public static class DependencyInjection
{
  #region Methods

  /// <summary>
  ///   Registers the job claim dialect of PostgreSQL (<c>FOR UPDATE SKIP LOCKED</c>), keyed by
  ///   <see cref="DatabaseProvider.PostgreSql" />.
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
  public static IServiceCollection AddPostgreSqlProvider(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddKeyedSingleton<IJobClaimDialect, PostgreSqlJobClaimDialect>(DatabaseProvider.PostgreSql);

    return services;
  }

  #endregion
}
