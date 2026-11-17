// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsTokenProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Infrastructure.AzureDevOps;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsTokenProviderTests : IDisposable
{
  #region Fields

  private static readonly string TokenPath = string.Create(CultureInfo.InvariantCulture, $"/{AzureDevOpsApiHarness.TenantId:D}/oauth2/v2.0/token");

  private readonly AzureDevOpsApiHarness _azure = new();

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetTokenAsync_SameTenant_ShouldReuseTheCachedToken()
  {
    GivenToken("eyJ.first");

    var first = await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);
    var second = await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);

    first.ShouldBe("eyJ.first");
    second.ShouldBe("eyJ.first");
    _azure.Server.LogEntries.Count().ShouldBe(1);
  }

  [Fact]
  public async Task GetTokenAsync_SecretChanged_ShouldResetTheCache()
  {
    GivenToken("eyJ.token");

    await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);
    _azure.Options.ClientSecret = AzureDevOpsApiHarness.ProtectedPrefix + "secret-2";
    await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);

    var bodies = _azure.Server.LogEntries.Select(static e => e.RequestMessage!.Body!).ToList();

    bodies.Count.ShouldBe(2);
    bodies[0].ShouldContain("client_secret=secret-1");
    bodies[1].ShouldContain("client_secret=secret-2");
  }

  [Fact]
  public async Task GetTokenAsync_ShouldAskForTheAzureDevOpsScopeOnTheTenantOfTheConnection()
  {
    GivenToken("eyJ.scope");

    await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);

    var request = _azure.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!;
    var form = request.Body!.Split('&').Select(Uri.UnescapeDataString).ToList();

    request.Path.ShouldBe(TokenPath);
    form.ShouldContain("grant_type=client_credentials");
    form.ShouldContain($"client_id={AzureDevOpsApiHarness.ClientId}");
    form.ShouldContain($"scope={AzureDevOpsTokenProvider.Scope}");
  }

  [Fact]
  public async Task GetTokenAsync_SuspendedConnection_ShouldThrowUnavailableWithoutAskingEntra()
  {
    GivenToken("eyJ.suspended");
    _azure.Connection.Suspend(AzureDevOpsApiHarness.Now);

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation)
    );

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
    _azure.Server.LogEntries.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetTokenAsync_CertificateConfigured_ShouldSendASignedClientAssertion()
  {
    GivenToken("eyJ.certificate");
    using var certificate = CreateCertificate("CN=CodeCoverage test");
    _azure.Options.ClientSecret = null;
    _azure.Options.CertificatePem = AzureDevOpsApiHarness.ProtectedPrefix + ExportPem(certificate);

    var token = await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);

    var form = ReadForm(_azure.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!.Body!);
    var audience = _azure.Options.GetTokenEndpoint(AzureDevOpsApiHarness.TenantId).AbsoluteUri;

    token.ShouldBe("eyJ.certificate");
    form.ShouldNotContainKey("client_secret");
    form["client_assertion_type"].ShouldBe("urn:ietf:params:oauth:client-assertion-type:jwt-bearer");

    using var publicCertificate = X509CertificateLoader.LoadCertificate(certificate.RawData);
    var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
      form["client_assertion"],
      new TokenValidationParameters
      {
        IssuerSigningKey = new X509SecurityKey(publicCertificate),
        ValidIssuer = AzureDevOpsApiHarness.ClientId,
        ValidAudience = audience,
        ValidateLifetime = false,
        RequireSignedTokens = true,
      }
    );

    validation.IsValid.ShouldBeTrue(validation.Exception?.Message);

    var assertion = (JsonWebToken)validation.SecurityToken;

    assertion.Alg.ShouldBe(SecurityAlgorithms.RsaSha256);
    assertion.X5t.ShouldBe(Base64UrlEncoder.Encode(certificate.GetCertHash()));
    assertion.Subject.ShouldBe(AzureDevOpsApiHarness.ClientId);
    assertion.Id.ShouldNotBeNullOrWhiteSpace();
    assertion.ValidFrom.ShouldBe(AzureDevOpsApiHarness.Now.UtcDateTime);
    assertion.ValidTo.ShouldBe(AzureDevOpsApiHarness.Now.AddMinutes(10).UtcDateTime);
  }

  [Fact]
  public async Task GetTokenAsync_CertificateChanged_ShouldResetTheCache()
  {
    GivenToken("eyJ.token");
    using var first = CreateCertificate("CN=CodeCoverage first");
    using var second = CreateCertificate("CN=CodeCoverage second");
    _azure.Options.ClientSecret = null;
    _azure.Options.CertificatePem = AzureDevOpsApiHarness.ProtectedPrefix + ExportPem(first);

    await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);
    await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);
    _azure.Options.CertificatePem = AzureDevOpsApiHarness.ProtectedPrefix + ExportPem(second);
    await _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation);

    var thumbprints = _azure.Server.LogEntries
                            .Select(static e => new JsonWebToken(ReadForm(e.RequestMessage!.Body!)["client_assertion"]).X5t)
                            .ToList();

    thumbprints.ShouldBe([Base64UrlEncoder.Encode(first.GetCertHash()), Base64UrlEncoder.Encode(second.GetCertHash())]);
  }

  [Fact]
  public async Task GetTokenAsync_Refused_ShouldThrowUnavailable()
  {
    _azure.Server.Given(Request.Create().WithPath(TokenPath).UsingPost())
          .RespondWith(Response.Create().WithStatusCode(401).WithBody("""{"error":"invalid_client"}"""));

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _azure.TokenProvider.GetTokenAsync(AzureDevOpsApiHarness.ConnectionId, Cancellation)
    );

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
    exception.Message.ShouldNotContain("secret-1");
  }

  private static X509Certificate2 CreateCertificate(string subject)
  {
    using var key = RSA.Create(2048);
    var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    return request.CreateSelfSigned(AzureDevOpsApiHarness.Now.AddDays(-1), AzureDevOpsApiHarness.Now.AddYears(1));
  }

  private static string ExportPem(X509Certificate2 certificate)
  {
    using var key = certificate.GetRSAPrivateKey()!;

    return certificate.ExportCertificatePem() + Environment.NewLine + key.ExportPkcs8PrivateKeyPem();
  }

  private static Dictionary<string, string> ReadForm(string body) =>
    body.Split('&')
        .Select(static field => field.Split('=', 2))
        .ToDictionary(static pair => Uri.UnescapeDataString(pair[0]), static pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')), StringComparer.Ordinal);

  private void GivenToken(string token)
  {
    _azure.Server.Given(Request.Create().WithPath(TokenPath).UsingPost())
          .RespondWith(
            Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody($$"""{"token_type":"Bearer","expires_in":3599,"ext_expires_in":3599,"access_token":"{{token}}"}""")
          );
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _azure.Dispose();
  }

  #endregion
}
