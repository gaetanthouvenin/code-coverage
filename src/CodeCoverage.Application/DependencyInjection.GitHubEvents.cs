// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.GitHubEvents.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Services;

namespace CodeCoverage.Application;

/// <content>Registers the services of the GitHub events area.</content>
public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the services of the GitHub events area, each with its explicit lifetime.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddGitHubEventServices(this IServiceCollection services)
  {
    services.AddScoped<RepositoryReconciler>();
    services.AddScoped<InstallationLifecycle>();

    return services;
  }

  #endregion
}