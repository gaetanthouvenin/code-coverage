// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubOidcTokenValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Identity;

// Every rejection starts from a token that passes and changes one thing, so that the rule under test is the one that
// rejects it. The discovery document and the keys are served by WireMock; the issuer stays the real one.
public sealed class GitHubOidcTokenValidatorTests : IDisposable
{
  #region Fields

  private const string Audience = "https://coverage-api.example.com";

  private const string DiscoveryPath = "/.well-known/openid-configuration";

  private const string JwksPath = "/.well-known/jwks";

  private const string KeyId = "trusted-key";

  // Cost of one discovery document and one key set.
  private const int MetadataCalls = 2;

  private static readonly RSA TrustedKey = RSA.Create(2048);

  private static readonly RSA OtherKey = RSA.Create(2048);

  private static readonly JsonWebTokenHandler TokenFactory = new() { SetDefaultTimesOnTokenCreation = false };

  private string? _apiPublicUrl;

  private readonly UploadOptions _options = new() { OidcAudience = Audience };

  private readonly CapturingLogger<GitHubOidcTokenValidator> _logger = new();

  private readonly WireMockServer _server = WireMockServer.Start();

  private readonly HttpClient _httpClient = new();

  private readonly GitHubOidcTokenValidator _validator;

  #endregion

  #region Ctors

  public GitHubOidcTokenValidatorTests()
  {
    GivenMetadata(GitHubOidcTokenValidator.Issuer, (TrustedKey, KeyId));

    var monitor = A.Fake<IOptionsMonitor<UploadOptions>>();
    A.CallTo(() => monitor.CurrentValue).ReturnsLazily(() => _options);

    var configuration = A.Fake<IConfiguration>();
    A.CallTo(() => configuration[SettingDefinitions.UrlsApiPublicUrl]).ReturnsLazily(() => _apiPublicUrl);

    _validator = new GitHubOidcTokenValidator(
      new ConfigurationManager<OpenIdConnectConfiguration>(
        $"{_server.Url}{DiscoveryPath}",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever(_httpClient) { RequireHttps = false }
      ),
      monitor,
      configuration,
      _logger
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  private int NetworkCalls => _server.LogEntries.Count();

  #endregion

  #region Methods

  [Fact]
  public async Task Validate_Valid_ShouldReturnRepositoryId()
  {
    var identity = await _validator.ValidateAsync(CreateToken(), Cancellation);

    identity.ShouldNotBeNull();
    identity.RepositoryId.ShouldBe("74");
    identity.RepositoryOwnerId.ShouldBe("65");
    identity.Repository.ShouldBe("octo-org/octo-repo");
    identity.RepositoryOwner.ShouldBe("octo-org");
    identity.Ref.ShouldBe("refs/heads/main");
    identity.EventName.ShouldBe("push");
  }

  [Fact]
  public async Task Validate_WithoutRefAndEventName_ShouldReturnThemAsNull()
  {
    var token = CreateToken(
      static claims =>
      {
        claims.Remove("ref");
        claims.Remove("event_name");
      }
    );

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldNotBeNull();
    identity.Ref.ShouldBeNull();
    identity.EventName.ShouldBeNull();
  }

  [Theory]
  [InlineData("https://github.com/octo-org")]
  [InlineData("https://coverage-api.example.com.attacker.test")]
  [InlineData("https://COVERAGE-API.example.com")]
  [InlineData("")]
  public async Task Validate_WrongAudience_ShouldFail(string audience)
  {
    var identity = await _validator.ValidateAsync(CreateToken(claims => claims["aud"] = audience), Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_WithoutAudience_ShouldFail()
  {
    var identity = await _validator.ValidateAsync(CreateToken(static claims => claims.Remove("aud")), Cancellation);

    identity.ShouldBeNull();
  }

  [Theory]
  [InlineData("https://issuer.attacker.test")]
  [InlineData("https://token.actions.githubusercontent.com/attacker")]
  [InlineData("https://token.actions.githubusercontent.com.attacker.test")]
  [InlineData("http://token.actions.githubusercontent.com")]
  [InlineData("HTTPS://TOKEN.ACTIONS.GITHUBUSERCONTENT.COM")]
  [InlineData("")]
  public async Task Validate_WrongIssuer_ShouldFail(string issuer)
  {
    var identity = await _validator.ValidateAsync(CreateToken(claims => claims["iss"] = issuer), Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_WithoutIssuer_ShouldFail()
  {
    var identity = await _validator.ValidateAsync(CreateToken(static claims => claims.Remove("iss")), Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_IssuerAnnouncedByTheDiscoveryDocument_ShouldFail()
  {
    const string Announced = "https://announced.attacker.test";
    GivenMetadata(Announced, (TrustedKey, KeyId));

    var identity = await _validator.ValidateAsync(CreateToken(static claims => claims["iss"] = Announced), Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_Expired_ShouldFail()
  {
    var token = CreateToken(static claims => claims["exp"] = FromNow(TimeSpan.FromMinutes(-2)));

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_ExpiredInsideTheClockTolerance_ShouldPass()
  {
    var token = CreateToken(
      static claims =>
      {
        claims["nbf"] = FromNow(TimeSpan.FromMinutes(-5));
        claims["exp"] = FromNow(TimeSpan.FromSeconds(-30));
      }
    );

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldNotBeNull();
  }

  [Fact]
  public async Task Validate_WithoutExpiry_ShouldFail()
  {
    var identity = await _validator.ValidateAsync(CreateToken(static claims => claims.Remove("exp")), Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_NotYetValid_ShouldFail()
  {
    var token = CreateToken(static claims => claims["nbf"] = FromNow(TimeSpan.FromMinutes(2)));

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldBeNull();
  }

  [Theory]
  [InlineData(SecurityAlgorithms.RsaSha384)]
  [InlineData(SecurityAlgorithms.RsaSha512)]
  [InlineData(SecurityAlgorithms.RsaSsaPssSha256)]
  public async Task Validate_AlgorithmOtherThanRs256SignedByTheTrustedKey_ShouldFail(string algorithm)
  {
    var token = Sign(ValidClaims(), new RsaSecurityKey(TrustedKey) { KeyId = KeyId }, algorithm);

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_HmacSignedWithThePublicKey_ShouldFail()
  {
    var key = new SymmetricSecurityKey(TrustedKey.ExportRSAPublicKey()) { KeyId = KeyId };

    var identity = await _validator.ValidateAsync(Sign(ValidClaims(), key, SecurityAlgorithms.HmacSha256), Cancellation);

    identity.ShouldBeNull();
  }

  [Theory]
  [InlineData("none")]
  [InlineData("None")]
  [InlineData("NONE")]
  public async Task Validate_UnsignedToken_ShouldFail(string algorithm)
  {
    var header = new Dictionary<string, string> { ["alg"] = algorithm, ["typ"] = "JWT", ["kid"] = KeyId };

    var identity = await _validator.ValidateAsync($"{Encode(header)}.{Encode(ValidClaims())}.", Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_SignatureRemoved_ShouldFail()
  {
    var token = CreateToken();

    var identity = await _validator.ValidateAsync(token[..(token.LastIndexOf('.') + 1)], Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_SignedByAnotherKeyUnderTheTrustedKeyId_ShouldFail()
  {
    var token = Sign(ValidClaims(), new RsaSecurityKey(OtherKey) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldBeNull();
  }

  [Theory]
  [InlineData("unknown-key")]
  [InlineData(null)]
  public async Task Validate_SignedByTheTrustedKeyUnderAnotherKeyId_ShouldFail(string? keyId)
  {
    var token = Sign(ValidClaims(), new RsaSecurityKey(TrustedKey) { KeyId = keyId }, SecurityAlgorithms.RsaSha256);

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_KeyEmbeddedInTheHeader_ShouldFail()
  {
    var parameters = OtherKey.ExportParameters(false);
    var embedded = new Dictionary<string, object>
    {
      ["kty"] = "RSA",
      ["n"] = Base64Url.EncodeToString(parameters.Modulus),
      ["e"] = Base64Url.EncodeToString(parameters.Exponent),
      ["kid"] = "embedded",
    };
    var token = Sign(
      ValidClaims(),
      new RsaSecurityKey(OtherKey) { KeyId = "embedded" },
      SecurityAlgorithms.RsaSha256,
      new Dictionary<string, object> { ["jwk"] = embedded }
    );

    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_PayloadChangedAfterSigning_ShouldFail()
  {
    var segments = CreateToken().Split('.');
    var forged = ValidClaims();
    forged["repository_id"] = "999";

    var identity = await _validator.ValidateAsync($"{segments[0]}.{Encode(forged)}.{segments[2]}", Cancellation);

    identity.ShouldBeNull();
  }

  [Theory]
  [InlineData("repository")]
  [InlineData("repository_id")]
  [InlineData("repository_owner")]
  [InlineData("repository_owner_id")]
  public async Task Validate_WithoutARepositoryClaim_ShouldFail(string claim)
  {
    var identity = await _validator.ValidateAsync(CreateToken(claims => claims.Remove(claim)), Cancellation);

    identity.ShouldBeNull();
  }

  [Theory]
  [InlineData("repository_id")]
  [InlineData("repository_owner_id")]
  public async Task Validate_BlankIdentifier_ShouldFail(string claim)
  {
    var identity = await _validator.ValidateAsync(CreateToken(claims => claims[claim] = " "), Cancellation);

    identity.ShouldBeNull();
  }

  [Theory]
  [InlineData("")]
  [InlineData("not-a-token")]
  [InlineData("a.b")]
  public async Task Validate_Malformed_ShouldFailWithoutCallingTheNetwork(string token)
  {
    var identity = await _validator.ValidateAsync(token, Cancellation);

    identity.ShouldBeNull();
    NetworkCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Validate_ThreeSegmentsOfGarbage_ShouldFail()
  {
    var identity = await _validator.ValidateAsync("a.b.c", Cancellation);

    identity.ShouldBeNull();
  }

  [Fact]
  public async Task Validate_NullToken_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _validator.ValidateAsync(null!, Cancellation));
  }

  [Fact]
  public async Task Validate_Repeatedly_ShouldCallTheNetworkOnlyToFillTheCache()
  {
    for (var i = 0; i < 5; i++)
    {
      (await _validator.ValidateAsync(CreateToken(), Cancellation)).ShouldNotBeNull();
    }

    NetworkCalls.ShouldBe(MetadataCalls);
  }

  [Fact]
  public async Task Validate_RejectedWithAKnownKey_ShouldNotRefreshTheKeys()
  {
    await _validator.ValidateAsync(CreateToken(), Cancellation);

    await _validator.ValidateAsync(Sign(ValidClaims(), new RsaSecurityKey(OtherKey) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256), Cancellation);
    await _validator.ValidateAsync(CreateToken(claims => claims["aud"] = "https://github.com/octo-org"), Cancellation);
    await Task.Delay(200, Cancellation);

    NetworkCalls.ShouldBe(MetadataCalls);
  }

  [Fact]
  public async Task Validate_KeyRotated_ShouldAcceptTheNewKeyOnceRefreshed()
  {
    await _validator.ValidateAsync(CreateToken(), Cancellation);
    GivenMetadata(GitHubOidcTokenValidator.Issuer, (TrustedKey, KeyId), (OtherKey, "rotated-key"));
    var token = Sign(ValidClaims(), new RsaSecurityKey(OtherKey) { KeyId = "rotated-key" }, SecurityAlgorithms.RsaSha256);

    await WaitUntilAsync(async () => await _validator.ValidateAsync(token, Cancellation) is not null);

    NetworkCalls.ShouldBe(2 * MetadataCalls);
  }

  [Fact]
  public async Task Validate_StreamOfUnknownKeys_ShouldRefreshOnlyOnce()
  {
    await _validator.ValidateAsync(CreateToken(), Cancellation);
    var unknown = Sign(ValidClaims(), new RsaSecurityKey(OtherKey) { KeyId = "unknown-key" }, SecurityAlgorithms.RsaSha256);

    (await _validator.ValidateAsync(unknown, Cancellation)).ShouldBeNull();
    await WaitUntilAsync(() => Task.FromResult(NetworkCalls == 2 * MetadataCalls));

    for (var i = 0; i < 5; i++)
    {
      (await _validator.ValidateAsync(unknown, Cancellation)).ShouldBeNull();
    }

    await Task.Delay(200, Cancellation);
    NetworkCalls.ShouldBe(2 * MetadataCalls);
  }

  [Fact]
  public async Task Validate_KeysNeverObtained_ShouldThrowBadGateway()
  {
    _server.ResetMappings();

    var exception = await Should.ThrowAsync<BadGatewayException>(() => _validator.ValidateAsync(CreateToken(), Cancellation));

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
  }

  [Fact]
  public async Task Validate_CanceledWhileTheKeysAreUnknown_ShouldNotReportAnOutage()
  {
    using var canceled = new CancellationTokenSource();
    await canceled.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(() => _validator.ValidateAsync(CreateToken(), canceled.Token));
  }

  [Fact]
  public async Task Validate_AfterAnOutageAtStart_ShouldObtainTheKeys()
  {
    _server.ResetMappings();
    await Should.ThrowAsync<BadGatewayException>(() => _validator.ValidateAsync(CreateToken(), Cancellation));
    GivenMetadata(GitHubOidcTokenValidator.Issuer, (TrustedKey, KeyId));

    var identity = await _validator.ValidateAsync(CreateToken(), Cancellation);

    identity.ShouldNotBeNull();
  }

  [Fact]
  public async Task Validate_GitHubDownOnceCached_ShouldKeepValidating()
  {
    await _validator.ValidateAsync(CreateToken(), Cancellation);
    _server.ResetMappings();

    var identity = await _validator.ValidateAsync(CreateToken(), Cancellation);

    identity.ShouldNotBeNull();
  }

  [Fact]
  public async Task Validate_AudienceChanged_ShouldApplyAtOnce()
  {
    var token = CreateToken();
    (await _validator.ValidateAsync(token, Cancellation)).ShouldNotBeNull();

    _options.OidcAudience = "https://other.example.com";

    (await _validator.ValidateAsync(token, Cancellation)).ShouldBeNull();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("  ")]
  public async Task Validate_WithoutAudienceSetting_ShouldExpectThePublicUrlOfTheApi(string? setting)
  {
    _options.OidcAudience = setting;
    _apiPublicUrl = "https://public-api.example.com";

    var matching = CreateToken(static claims => claims["aud"] = "https://public-api.example.com");

    (await _validator.ValidateAsync(matching, Cancellation)).ShouldNotBeNull();
    (await _validator.ValidateAsync(CreateToken(), Cancellation)).ShouldBeNull();
  }

  [Fact]
  public async Task Validate_NoAudienceConfiguredAtAll_ShouldThrow()
  {
    _options.OidcAudience = null;

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => _validator.ValidateAsync(CreateToken(), Cancellation));

    exception.Message.ShouldContain(SettingDefinitions.UploadsOidcAudience);
  }

  [Fact]
  public async Task Validate_Rejected_ShouldLogWithoutTheExceptionOfTheLibrary()
  {
    await _validator.ValidateAsync(CreateToken(claims => claims["aud"] = "https://github.com/octo-org"), Cancellation);

    var entry = _logger.Entries.ShouldHaveSingleItem();
    entry.Level.ShouldBe(LogLevel.Information);
    entry.Exception.ShouldBeNull();
  }

  [Fact]
  public void CreateConfigurationManager_ShouldReadTheDiscoveryDocumentOfGitHub()
  {
    var manager = GitHubOidcTokenValidator.CreateConfigurationManager(_httpClient);

    manager.ShouldBeOfType<ConfigurationManager<OpenIdConnectConfiguration>>()
           .MetadataAddress.ShouldBe("https://token.actions.githubusercontent.com/.well-known/openid-configuration");
  }

  private static async Task WaitUntilAsync(Func<Task<bool>> condition)
  {
    var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

    while (!await condition())
    {
      DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "the condition did not become true in time");
      await Task.Delay(25, Cancellation);
    }
  }

  private static long FromNow(TimeSpan offset) => DateTimeOffset.UtcNow.Add(offset).ToUnixTimeSeconds();

  private static Dictionary<string, object> ValidClaims() =>
    new()
    {
      ["iss"] = GitHubOidcTokenValidator.Issuer,
      ["aud"] = Audience,
      ["iat"] = FromNow(TimeSpan.Zero),
      ["nbf"] = FromNow(TimeSpan.Zero),
      ["exp"] = FromNow(TimeSpan.FromMinutes(5)),
      ["jti"] = "example-id",
      ["sub"] = "repo:octo-org/octo-repo:ref:refs/heads/main",
      ["repository"] = "octo-org/octo-repo",
      ["repository_id"] = "74",
      ["repository_owner"] = "octo-org",
      ["repository_owner_id"] = "65",
      ["ref"] = "refs/heads/main",
      ["event_name"] = "push",
    };

  private static string CreateToken(Action<Dictionary<string, object>>? change = null)
  {
    var claims = ValidClaims();
    change?.Invoke(claims);

    return Sign(claims, new RsaSecurityKey(TrustedKey) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);
  }

  private static string Sign(
    Dictionary<string, object> claims,
    SecurityKey key,
    string algorithm,
    Dictionary<string, object>? headers = null) =>
    TokenFactory.CreateToken(
      new SecurityTokenDescriptor
      {
        Claims = claims,
        SigningCredentials = new SigningCredentials(key, algorithm),
        AdditionalHeaderClaims = headers,
      }
    );

  private static string Encode<T>(T value) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));

  private static Dictionary<string, string> ToJwk(RSA key, string keyId)
  {
    var parameters = key.ExportParameters(false);

    return new Dictionary<string, string>
    {
      ["kty"] = "RSA",
      ["alg"] = "RS256",
      ["use"] = "sig",
      ["kid"] = keyId,
      ["n"] = Base64Url.EncodeToString(parameters.Modulus),
      ["e"] = Base64Url.EncodeToString(parameters.Exponent),
    };
  }

  private void GivenMetadata(string issuer, params (RSA Key, string KeyId)[] keys)
  {
    _server.ResetMappings();
    GivenJson(
      DiscoveryPath,
      new Dictionary<string, object>
      {
        ["issuer"] = issuer,
        ["jwks_uri"] = $"{_server.Url}{JwksPath}",
        ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
      }
    );
    GivenJson(JwksPath, new Dictionary<string, object> { ["keys"] = keys.Select(static k => ToJwk(k.Key, k.KeyId)).ToArray() });
  }

  private void GivenJson(string path, Dictionary<string, object> body)
  {
    _server.Given(Request.Create().WithPath(path).UsingGet())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(200)
                     .WithHeader("Content-Type", "application/json")
                     .WithBody(JsonSerializer.Serialize(body))
           );
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _httpClient.Dispose();
    _server.Stop();
    _server.Dispose();
  }

  #endregion
}
