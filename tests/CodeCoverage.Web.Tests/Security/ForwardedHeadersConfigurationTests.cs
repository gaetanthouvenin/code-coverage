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

using CodeCoverage.Web.Configuration.Security;

namespace CodeCoverage.Web.Tests.Security;

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

  private static IConfiguration Configuration(Dictionary<string, string?> values) =>
    new ConfigurationBuilder().AddInMemoryCollection(values).Build();

  #endregion
}
