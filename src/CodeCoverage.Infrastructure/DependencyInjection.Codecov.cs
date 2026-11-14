// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Codecov.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Codecov;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Fields

  /// <summary>The API of Codecov, the only one the import reads (M5.06).</summary>
  private static readonly Uri CodecovApiAddress = new("https://api.codecov.io/");

  #endregion

  #region Methods

  /// <summary>Registers the client of the API of Codecov, read by the import of the history only.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>
  ///   The standard resilience handler retries a 5xx, a 408, a 429 or a timeout; a 401, a 403 or a 404 is a refusal for
  ///   good, which the client returns as such.
  /// </remarks>
  private static IServiceCollection AddCodecov(this IServiceCollection services)
  {
    services.AddHttpClient<ICodecovApiClient, CodecovApiClient>(
              static client =>
              {
                client.BaseAddress = CodecovApiAddress;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CodeCoverage");
              }
            )
            .AddStandardResilienceHandler();

    return services;
  }

  #endregion
}
