// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Notifications.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Services;

namespace CodeCoverage.Application;

/// <content>Registers the services of the notifications area.</content>
public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the services of the notifications area, each with its explicit lifetime.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddNotificationServices(this IServiceCollection services)
  {
    services.AddScoped<NotificationService>();

    return services;
  }

  #endregion
}
