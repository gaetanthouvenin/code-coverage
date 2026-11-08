// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Repositories.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the repositories of <c>Persistence/Repositories</c>, the database adapters of the repository ports.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddRepositories(this IServiceCollection services)
  {
    return services;
  }

  #endregion
}
