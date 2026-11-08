// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Commits.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace CodeCoverage.Application;

/// <content>Registers the services of the commits area.</content>
public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the services of the commits area, each with its explicit lifetime.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddCommitServices(this IServiceCollection services)
  {
    return services;
  }

  #endregion
}
