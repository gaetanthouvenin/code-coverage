// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsApiHarness.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using WireMock.Server;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.AzureDevOps;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.Tests.Support;

// The real azure-devops and Entra ID pipelines in front of one WireMock server, which plays both the API and the
// authority: the retries do not wait, and a value is "protected" by a prefix, so that a test can tell which secret
// was decrypted.
internal sealed class AzureDevOpsApiHarness : IDisposable
{
  #region Fields

  public const string ClientId = "11111111-2222-3333-4444-555555555555";

  public const string ConnectionId = "contoso";

  public const long OrganizationId = 7;

  public const string ProtectedPrefix = "protected:";

  public static readonly Guid TenantId = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

  public static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

  private readonly ServiceProvider _services;

  private readonly IServiceScope _scope;

  #endregion

  #region Ctors

  public AzureDevOpsApiHarness()
  {
    Server = WireMockServer.Start();
    Options = new AzureDevOpsOptions
    {
      ApiBaseUrl = Server.Url!,
      Authority = Server.Url!,
      ClientId = ClientId,
      ClientSecret = ProtectedPrefix + "secret-1",
    };
    Connection = ForgeConnection.Create(
      ConnectionId,
      OrganizationId,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      "0f6b6c4e-3d1a-4a52-9b0e-3c2d1e0f9a8b",
      new Uri("https://dev.azure.com/contoso/"),
      TenantId,
      null,
      ProtectedPrefix + "entra-application",
      ProtectedPrefix + "webhook",
      null,
      Now
    );

    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Unprotect(A<string>._)).ReturnsLazily((string value) => value[ProtectedPrefix.Length..]);

    var monitor = A.Fake<IOptionsMonitor<AzureDevOpsOptions>>();
    A.CallTo(() => monitor.CurrentValue).ReturnsLazily(() => Options);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var connections = A.Fake<IForgeConnectionRepository>();
    A.CallTo(() => connections.FindAsync(ConnectionId, A<CancellationToken>._)).ReturnsLazily(() => Connection);

    var organization = A.Fake<ICurrentOrganization>();
    A.CallTo(() => organization.IsKnown).Returns(true);
    A.CallTo(() => organization.Id).Returns(OrganizationId);

    var services = new ServiceCollection().AddLogging().AddMemoryCache();

    services.AddSingleton(protector).AddSingleton(monitor).AddSingleton(clock).AddSingleton(connections).AddSingleton(organization);
    services.AddScoped<ForgeConnectionCredentialReader>();
    services.AddScoped<AzureDevOpsTokenProvider>();
    services.AddAzureDevOpsHttpClient();

    foreach (var client in new[] { AzureDevOpsHttpClientConfiguration.ClientName, AzureDevOpsHttpClientConfiguration.EntraClientName })
    {
      services.PostConfigure<HttpStandardResilienceOptions>(
        $"{client}-standard",
        static resilience =>
        {
          resilience.Retry.Delay = TimeSpan.Zero;
          resilience.Retry.UseJitter = false;
        }
      );
    }

    _services = services.BuildServiceProvider(validateScopes: true);
    _scope = _services.CreateScope();
  }

  #endregion

  #region Properties

  public ForgeConnection Connection { get; }

  public AzureDevOpsOptions Options { get; }

  public WireMockServer Server { get; }

  public AzureDevOpsTokenProvider TokenProvider => _scope.ServiceProvider.GetRequiredService<AzureDevOpsTokenProvider>();

  #endregion

  #region Methods

  public HttpClient CreateClient() =>
    _services.GetRequiredService<IHttpClientFactory>().CreateClient(AzureDevOpsHttpClientConfiguration.ClientName);

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _scope.Dispose();
    _services.Dispose();
    Server.Stop();
    Server.Dispose();
  }

  #endregion
}
