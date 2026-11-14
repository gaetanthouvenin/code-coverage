// ------------------------------------------------------------------------------------------------
// <copyright file="ForwardedHeadersConfigurationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Api.Configuration.Security;

namespace CodeCoverage.Api.Tests.Security;

public sealed class ForwardedHeadersConfigurationTests
{
  #region Methods

  [Fact]
  public void ReadKnownProxies_SeparatedValue_ShouldParseEachAddress()
  {
    var configuration = Configuration(new() { [ForwardedHeadersConfiguration.KnownProxiesKey] = "10.0.0.2, 10.0.0.3;fd00::1" });

    ForwardedHeadersConfiguration.ReadKnownProxies(configuration)
                                 .ShouldBe([IPAddress.Parse("10.0.0.2"), IPAddress.Parse("10.0.0.3"), IPAddress.Parse("fd00::1")]);
  }

  [Fact]
  public void ReadKnownProxies_List_ShouldParseEachAddress()
  {
    var configuration = Configuration(new()
    {
      [$"{ForwardedHeadersConfiguration.KnownProxiesKey}:0"] = "10.0.0.2",
      [$"{ForwardedHeadersConfiguration.KnownProxiesKey}:1"] = "10.0.0.3",
    });

    ForwardedHeadersConfiguration.ReadKnownProxies(configuration)
                                 .ShouldBe([IPAddress.Parse("10.0.0.2"), IPAddress.Parse("10.0.0.3")]);
  }

  [Fact]
  public void ReadKnownProxies_InvalidAddress_ShouldThrow()
  {
    var configuration = Configuration(new() { [ForwardedHeadersConfiguration.KnownProxiesKey] = "caddy" });

    Should.Throw<InvalidOperationException>(() => ForwardedHeadersConfiguration.ReadKnownProxies(configuration))
          .Message.ShouldContain("'caddy'");
  }

  [Fact]
  public void AddForwardedHeaders_ShouldTrustTheLoopbackAndTheConfiguredProxiesOnly()
  {
    var configuration = Configuration(new() { [ForwardedHeadersConfiguration.KnownProxiesKey] = "10.0.0.2" });
    using var provider = new ServiceCollection().AddForwardedHeaders(configuration).BuildServiceProvider();

    var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

    options.KnownProxies.ShouldContain(IPAddress.Parse("10.0.0.2"));
    options.KnownProxies.ShouldContain(IPAddress.IPv6Loopback);
    options.ForwardedHeaders.ShouldBe(
      Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
      | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto);
  }

  [Fact]
  public void ReadKnownNetworks_SeparatedValue_ShouldParseEachNetwork()
  {
    var configuration = Configuration(new() { [ForwardedHeadersConfiguration.KnownNetworksKey] = "172.30.0.0/24, 10.0.0.0/8;fd00::/64" });

    ForwardedHeadersConfiguration.ReadKnownNetworks(configuration)
                                 .ShouldBe([System.Net.IPNetwork.Parse("172.30.0.0/24"), System.Net.IPNetwork.Parse("10.0.0.0/8"), System.Net.IPNetwork.Parse("fd00::/64")]);
  }

  [Fact]
  public void ReadKnownNetworks_List_ShouldParseEachNetwork()
  {
    var configuration = Configuration(new()
    {
      [$"{ForwardedHeadersConfiguration.KnownNetworksKey}:0"] = "172.30.0.0/24",
      [$"{ForwardedHeadersConfiguration.KnownNetworksKey}:1"] = " 10.0.0.0/8 ",
    });

    ForwardedHeadersConfiguration.ReadKnownNetworks(configuration)
                                 .ShouldBe([System.Net.IPNetwork.Parse("172.30.0.0/24"), System.Net.IPNetwork.Parse("10.0.0.0/8")]);
  }

  [Fact]
  public void ReadKnownNetworks_Missing_ShouldBeEmpty()
  {
    ForwardedHeadersConfiguration.ReadKnownNetworks(Configuration(new())).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("npx")]
  [InlineData("172.30.0.10")]
  [InlineData("172.30.0.10/24")]
  [InlineData("172.30.0.0/33")]
  public void ReadKnownNetworks_InvalidNetwork_ShouldThrow(string value)
  {
    var configuration = Configuration(new() { [ForwardedHeadersConfiguration.KnownNetworksKey] = value });

    Should.Throw<InvalidOperationException>(() => ForwardedHeadersConfiguration.ReadKnownNetworks(configuration))
          .Message.ShouldContain($"'{value}'");
  }

  [Fact]
  public void AddForwardedHeaders_KnownNetworks_ShouldTrustTheConfiguredNetworks()
  {
    var configuration = Configuration(new() { [ForwardedHeadersConfiguration.KnownNetworksKey] = "172.30.0.0/24" });
    using var provider = new ServiceCollection().AddForwardedHeaders(configuration).BuildServiceProvider();

    var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

    options.KnownIPNetworks.ShouldContain(System.Net.IPNetwork.Parse("172.30.0.0/24"));
    options.KnownIPNetworks.ShouldContain(System.Net.IPNetwork.Parse("127.0.0.0/8"));
  }

  private static IConfiguration Configuration(Dictionary<string, string?> values) =>
    new ConfigurationBuilder().AddInMemoryCollection(values).Build();

  #endregion
}
