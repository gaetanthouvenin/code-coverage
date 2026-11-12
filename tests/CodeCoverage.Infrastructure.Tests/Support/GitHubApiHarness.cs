// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubApiHarness.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WireMock.Server;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Tests.Support;

// The real github pipeline in front of a WireMock server: the retries do not wait, and a value is
// "protected" by a prefix, so that a test can tell that the private key is decrypted at the moment of use.
internal sealed class GitHubApiHarness : IDisposable
{
  #region Fields

  public const string ClientId = "Iv1.test-client";

  public const string ProtectedPrefix = "protected:";

  public static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

  private readonly TestMeterFactory _meters = new();

  private readonly ServiceProvider _services;

  #endregion

  #region Ctors

  public GitHubApiHarness()
  {
    Server = WireMockServer.Start();
    Options = new GitHubOptions { ApiBaseUrl = Server.Url!, ClientId = ClientId };
    Quota = new MeasurementRecorder(_meters);

    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Unprotect(A<string>._)).ReturnsLazily((string value) => value[ProtectedPrefix.Length..]);

    var monitor = A.Fake<IOptionsMonitor<GitHubOptions>>();
    A.CallTo(() => monitor.CurrentValue).ReturnsLazily(() => Options);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var services = new ServiceCollection().AddLogging().AddMemoryCache();

    services.AddSingleton(protector).AddSingleton(monitor).AddSingleton(clock);
    services.AddSingleton(new CodeCoverageMeter(_meters));
    services.AddSingleton<GitHubAppTokenProvider>();
    services.AddSingleton<ILogger<GitHubAppGateway>>(GatewayLog);
    services.AddGitHubHttpClient();
    services.PostConfigure<HttpStandardResilienceOptions>(
      $"{GitHubHttpClientConfiguration.ClientName}-standard",
      static resilience =>
      {
        resilience.Retry.Delay = TimeSpan.Zero;
        resilience.Retry.UseJitter = false;
      }
    );

    _services = services.BuildServiceProvider();
  }

  #endregion

  #region Properties

  public CapturingLogger<GitHubAppGateway> GatewayLog { get; } = new();

  public GitHubOptions Options { get; }

  public MeasurementRecorder Quota { get; }

  public WireMockServer Server { get; }

  public GitHubAppTokenProvider TokenProvider => _services.GetRequiredService<GitHubAppTokenProvider>();

  public GitHubAppGateway Gateway => _services.GetRequiredService<GitHubAppGateway>();

  public GitHubUserGateway UserGateway => _services.GetRequiredService<GitHubUserGateway>();

  public GitHubQuotaTracker Quotas => _services.GetRequiredService<GitHubQuotaTracker>();

  #endregion

  #region Methods

  public static bool IsSignedBy(string jwt, RSA key)
  {
    var lastDot = jwt.LastIndexOf('.');

    return key.VerifyData(
      Encoding.ASCII.GetBytes(jwt[..lastDot]),
      Base64Url.DecodeFromChars(jwt.AsSpan(lastDot + 1)),
      HashAlgorithmName.SHA256,
      RSASignaturePadding.Pkcs1
    );
  }

  public HttpClient CreateClient() =>
    _services.GetRequiredService<IHttpClientFactory>().CreateClient(GitHubHttpClientConfiguration.ClientName);

  public void UsePrivateKey(RSA key) => Options.PrivateKeyPem = ProtectedPrefix + key.ExportRSAPrivateKeyPem();

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _services.Dispose();
    Quota.Dispose();
    _meters.Dispose();
    Server.Stop();
    Server.Dispose();
  }

  #endregion
}
