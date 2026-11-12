// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Access.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Services;

namespace CodeCoverage.Application;

/// <content>Registers the services of the access area.</content>
public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the services of the access area, each with its explicit lifetime.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>The writer of the memberships is scoped, like the repositories it writes through.</remarks>
  private static IServiceCollection AddAccessServices(this IServiceCollection services)
  {
    services.AddScoped<MembershipUpdater>();

    return services;
  }

  #endregion
}
