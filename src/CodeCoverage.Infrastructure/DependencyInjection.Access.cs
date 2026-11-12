// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Access.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the adapters of the access area: the access of the users to the repositories.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>The access service is scoped: it keeps the rows it read for the scope, never longer.</remarks>
  private static IServiceCollection AddAccess(this IServiceCollection services)
  {
    services.AddScoped<IRepositoryAccessService, RepositoryAccessService>();

    return services;
  }

  #endregion
}
