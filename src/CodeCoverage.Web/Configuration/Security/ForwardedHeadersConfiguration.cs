// ------------------------------------------------------------------------------------------------
// <copyright file="ForwardedHeadersConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

using CodeCoverage.Domain.Guards;

using IPNetwork = System.Net.IPNetwork;

namespace CodeCoverage.Web.Configuration.Security;

/// <summary>Trusts the <c>X-Forwarded-*</c> headers of the known reverse proxies only.</summary>
/// <remarks>
///   The same rule as the Api, with the same bootstrap keys: behind the TLS proxy, the scheme of the request is the one
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

  /// <summary>
  ///   The configuration key of the reverse proxy networks, in CIDR notation: a list, or one value
  ///   separated by commas, semicolons or spaces (<c>ForwardedHeaders__KnownNetworks=172.30.0.0/24</c>).
  ///   A proxy whose address changes on each restart, such as a container on a shared Docker network,
  ///   is trusted through its network.
  /// </summary>
  public const string KnownNetworksKey = "ForwardedHeaders:KnownNetworks";

  private static readonly char[] Separators = [',', ';', ' '];

  #endregion

  #region Methods

  /// <summary>
  ///   Configures <see cref="ForwardedHeadersOptions" />: <c>X-Forwarded-For</c> and
  ///   <c>X-Forwarded-Proto</c> are read from the loopback and from the configured proxies and
  ///   networks only.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="services" /> or <paramref name="configuration" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="InvalidOperationException">
  ///   A configured proxy is not an IP address, or a configured network is not in CIDR notation.
  /// </exception>
  public static IServiceCollection AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    var proxies = ReadKnownProxies(configuration);
    var networks = ReadKnownNetworks(configuration);

    services.Configure<ForwardedHeadersOptions>(options =>
                                                {
                                                  options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                                                             | ForwardedHeaders.XForwardedProto;

                                                  foreach (var proxy in proxies)
                                                  {
                                                    options.KnownProxies.Add(proxy);
                                                  }

                                                  foreach (var network in networks)
                                                  {
                                                    options.KnownIPNetworks.Add(network);
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

    var proxies = new List<IPAddress>();

    foreach (var value in ReadValues(configuration, KnownProxiesKey))
    {
      InvalidOperationException.ThrowIf(!IPAddress.TryParse(value, out var address),
                                        $"{KnownProxiesKey}: '{value}' is not an IP address.");
      proxies.Add(address);
    }

    return proxies;
  }

  /// <summary>Reads the networks of the known reverse proxies.</summary>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The networks, empty when none is configured.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="configuration" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">
  ///   A configured network is not in CIDR notation, or has bits set after its prefix.
  /// </exception>
  internal static IReadOnlyList<IPNetwork> ReadKnownNetworks(IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var networks = new List<IPNetwork>();

    foreach (var value in ReadValues(configuration, KnownNetworksKey))
    {
      InvalidOperationException.ThrowIf(!IPNetwork.TryParse(value, out var network),
                                        $"{KnownNetworksKey}: '{value}' is not a network in CIDR notation (e.g. 172.30.0.0/24).");
      InvalidOperationException.ThrowIf(!IsBaseAddress(value, network),
                                        $"{KnownNetworksKey}: '{value}' has bits set after its prefix (e.g. 172.30.0.0/24, not 172.30.0.10/24).");
      networks.Add(network);
    }

    return networks;
  }

  /// <summary>
  ///   Tells whether a network is written with its base address: <see cref="IPNetwork.TryParse(string, out IPNetwork)" />
  ///   clears the bits after the prefix, so <c>172.30.0.10/24</c> would silently become <c>172.30.0.0/24</c>.
  /// </summary>
  /// <param name="value">The configured value.</param>
  /// <param name="network">The network parsed from it.</param>
  /// <returns><see langword="true" /> for <c>172.30.0.0/24</c>, <see langword="false" /> for <c>172.30.0.10/24</c>.</returns>
  private static bool IsBaseAddress(string value, IPNetwork network) =>
    IPAddress.TryParse(value.AsSpan(0, value.IndexOf('/', StringComparison.Ordinal)), out var address)
    && address.Equals(network.BaseAddress);

  /// <summary>Reads the values of a list key: a list, or one value separated by commas, semicolons or spaces.</summary>
  /// <param name="configuration">The application configuration.</param>
  /// <param name="key">The configuration key.</param>
  /// <returns>The trimmed, non-empty values.</returns>
  private static string[] ReadValues(IConfiguration configuration, string key)
  {
    var section = configuration.GetSection(key);

    return section.Value is { } single
             ? single.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             : section.GetChildren()
                      .Select(c => c.Value?.Trim())
                      .OfType<string>()
                      .Where(v => v.Length > 0)
                      .ToArray();
  }

  #endregion
}
