// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRateLimiterConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Configuration.RateLimiting;

/// <summary>Registers the limiter of the uploads.</summary>
public static class UploadRateLimiterConfiguration
{
  #region Methods

  /// <summary>Registers the limiter, shared by every request.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  public static IServiceCollection AddUploadRateLimiter(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    return services.AddSingleton<UploadRateLimiter>();
  }

  #endregion
}
