// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Uploads.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Services;

namespace CodeCoverage.Application;

/// <content>Registers the services of the uploads area.</content>
public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the services of the uploads area, each with its explicit lifetime.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>
  ///   A service that throttles the whole process, such as a <c>SemaphoreSlim</c> on the parallel
  ///   parses, must outlive the scope of a job: a scoped lifetime would give each job its own limit.
  /// </remarks>
  private static IServiceCollection AddUploadServices(this IServiceCollection services)
  {
    services.AddScoped<UploadTokenService>();

    return services;
  }

  #endregion
}
