// ------------------------------------------------------------------------------------------------
// <copyright file="ForwardedHeadersConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

namespace CodeCoverage.Web.Configuration.Security;

/// <summary>Trusts the <c>X-Forwarded-*</c> headers of the known reverse proxies only.</summary>
/// <remarks>
///   The same rule as the Api, with the same bootstrap key: behind the TLS proxy, the scheme of the request is the one
///   the proxy received, so that the sign-in with GitHub builds an <c>https</c> callback.
/// </remarks>
public static class ForwardedHeadersConfiguration
{
  #region Fields

  /// <summary>
  ///   The configuration key of the reverse proxy addresses: a list, or one value separated by
  ///   commas, semicolons or spaces (<c>ForwardedHeaders__KnownProxies=10.0.0.2,10.0.0.3</c>).
  /// </summary>
  public const string KnownProxiesKey = "ForwardedHeaders:KnownProxies";

  private static readonly char[] Separators = [',', ';', ' '];

  #endregion

  #region Methods

  /// <summary>
  ///   Configures <see cref="ForwardedHeadersOptions" />: <c>X-Forwarded-For</c> and
  ///   <c>X-Forwarded-Proto</c> are read from the loopback and from the configured proxies only.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="services" /> or <paramref name="configuration" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="InvalidOperationException">A configured proxy is not an IP address.</exception>
  public static IServiceCollection AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    var proxies = ReadKnownProxies(configuration);

    services.Configure<ForwardedHeadersOptions>(options =>
                                                {
                                                  options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                                                             | ForwardedHeaders.XForwardedProto;

                                                  foreach (var proxy in proxies)
                                                  {
                                                    options.KnownProxies.Add(proxy);
                                                  }
                                                });

    return services;
  }

  /// <summary>Reads the addresses of the known reverse proxies.</summary>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The addresses, empty when none is configured.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="configuration" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">A configured proxy is not an IP address.</exception>
  internal static IReadOnlyList<IPAddress> ReadKnownProxies(IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var section = configuration.GetSection(KnownProxiesKey);
    var values = section.Value is { } single
                   ? single.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   : section.GetChildren()
                            .Select(c => c.Value?.Trim())
                            .OfType<string>()
                            .Where(v => v.Length > 0)
                            .ToArray();

    return values.Select(v => IPAddress.TryParse(v, out var address)
                                ? address
                                : throw new InvalidOperationException($"{KnownProxiesKey}: '{v}' is not an IP address."))
                 .ToList();
  }

  #endregion
}
