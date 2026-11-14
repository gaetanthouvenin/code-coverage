// ------------------------------------------------------------------------------------------------
// <copyright file="ForwardedHeadersHostTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Web.Configuration.Security;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Security;

[Collection(CodeCoverageWebCollection.Name)]
public sealed class ForwardedHeadersHostTests(CodeCoverageWebFactory factory)
{
  #region Methods

  [Theory]
  [InlineData("172.30.0.0/24", "172.30.0.57", "https")]
  [InlineData("172.30.0.0/24", "172.31.0.57", "http")]
  [InlineData("", "172.30.0.57", "http")]
  public async Task Request_FromAProxyAddress_ShouldTrustXForwardedProtoOnlyInsideTheKnownNetworks(
    string knownNetworks,
    string remoteAddress,
    string expectedScheme)
  {
    await using var host = factory.WithWebHostBuilder(
      b => b.UseSetting(ForwardedHeadersConfiguration.KnownNetworksKey, knownNetworks)
            .ConfigureServices(s => s.AddSingleton<IStartupFilter>(new ForwardedSchemeProbe(IPAddress.Parse(remoteAddress)))));
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
    request.Headers.Add("X-Forwarded-Proto", "https");

    using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

    response.Headers.GetValues(ForwardedSchemeProbe.SeenSchemeHeader).ShouldBe([expectedScheme]);
  }

  [Fact]
  public async Task Start_WithAnInvalidKnownNetwork_ShouldRefuseToStart()
  {
    await using var host = factory.WithWebHostBuilder(
      b => b.UseSetting(ForwardedHeadersConfiguration.KnownNetworksKey, "172.30.0.10"));

    Should.Throw<InvalidOperationException>(() => host.CreateClient())
          .Message.ShouldContain(ForwardedHeadersConfiguration.KnownNetworksKey);
  }

  #endregion
}
