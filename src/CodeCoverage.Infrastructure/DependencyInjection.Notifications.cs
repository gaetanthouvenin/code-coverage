// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Notifications.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the adapters of the notifications area: the statuses and comments published on the pull requests.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddNotifications(this IServiceCollection services)
  {
    return services;
  }

  #endregion
}
