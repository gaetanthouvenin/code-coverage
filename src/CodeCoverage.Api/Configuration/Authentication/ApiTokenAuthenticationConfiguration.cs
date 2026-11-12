// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenAuthenticationConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;

namespace CodeCoverage.Api.Configuration.Authentication;

/// <summary>Registers the authentication of the management endpoints by personal tokens of the API.</summary>
public static class ApiTokenAuthenticationConfiguration
{
  #region Methods

  /// <summary>Registers the <c>ApiToken</c> scheme as the default one, and the authorization.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   No fallback policy is set: the uploads, the webhooks, the badges, the download of the uploader and the probes
  ///   stay anonymous, and each management endpoint requires the scheme by its own attribute.
  /// </remarks>
  public static IServiceCollection AddApiTokenAuthentication(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddAuthentication(ApiTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(ApiTokenAuthenticationHandler.SchemeName, null);
    services.AddAuthorization();

    return services;
  }

  #endregion
}
