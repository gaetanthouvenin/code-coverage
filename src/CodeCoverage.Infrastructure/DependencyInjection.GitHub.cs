// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.GitHub.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the adapters of the GitHub area: the gateways of the GitHub API and the webhook validation.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddGitHub(this IServiceCollection services)
  {
    return services;
  }

  #endregion
}
