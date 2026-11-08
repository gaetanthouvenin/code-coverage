// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppTokenProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;

using Microsoft.IdentityModel.JsonWebTokens;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubAppTokenProviderTests : IDisposable
{
  #region Fields

  private const long Installation = 42;

  private readonly GitHubApiHarness _github = new();

  private readonly RSA _key = RSA.Create(2048);

  #endregion

  #region Ctors

  public GitHubAppTokenProviderTests()
  {
    _github.UsePrivateKey(_key);
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetInstallationToken_Twice_ShouldCallOnce()
  {
    GivenToken(Installation, "ghs_first", TimeSpan.FromHours(1));

    var first = await _github.TokenProvider.GetInstallationTokenAsync(Installation, Cancellation);
    var second = await _github.TokenProvider.GetInstallationTokenAsync(Installation, Cancellation);

    first.ShouldBe("ghs_first");
    second.ShouldBe("ghs_first");
    _github.Server.LogEntries.Count().ShouldBe(1);
  }

  [Fact]
  public async Task GetInstallationToken_Concurrently_ShouldCallOnce()
  {
    GivenToken(Installation, "ghs_shared", TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(200));
    var provider = _github.TokenProvider;

    var tokens = await Task.WhenAll(
      Enumerable.Range(0, 10).Select(_ => provider.GetInstallationTokenAsync(Installation, Cancellation))
    );

    tokens.ShouldAllBe(static t => t == "ghs_shared");
    _github.Server.LogEntries.Count().ShouldBe(1);
  }

  [Fact]
  public async Task GetInstallationToken_InsideTheRenewalMargin_ShouldCallAgain()
  {
    GivenToken(Installation, "ghs_short", TimeSpan.FromMinutes(9));

    await _github.TokenProvider.GetInstallationTokenAsync(Installation, Cancellation);
    await _github.TokenProvider.GetInstallationTokenAsync(Installation, Cancellation);

    _github.Server.LogEntries.Count().ShouldBe(2);
  }

  [Fact]
  public async Task GetInstallationToken_TwoInstallations_ShouldCacheEachOnItsOwn()
  {
    GivenToken(1, "ghs_one", TimeSpan.FromHours(1));
    GivenToken(2, "ghs_two", TimeSpan.FromHours(1));

    var one = await _github.TokenProvider.GetInstallationTokenAsync(1, Cancellation);
    var two = await _github.TokenProvider.GetInstallationTokenAsync(2, Cancellation);

    one.ShouldBe("ghs_one");
    two.ShouldBe("ghs_two");
    _github.Server.LogEntries.Count().ShouldBe(2);
  }

  [Fact]
  public async Task GetInstallationToken_ShouldAuthenticateWithTheJwtOfTheApp()
  {
    GivenToken(Installation, "ghs_jwt", TimeSpan.FromHours(1));

    await _github.TokenProvider.GetInstallationTokenAsync(Installation, Cancellation);

    var request = _github.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!;
    var jwt = BearerOf(request.Headers!["Authorization"][0]);
    var token = new JsonWebToken(jwt);

    GitHubApiHarness.IsSignedBy(jwt, _key).ShouldBeTrue();
    token.Alg.ShouldBe("RS256");
    token.Issuer.ShouldBe(GitHubApiHarness.ClientId);
    token.IssuedAt.ShouldBe(GitHubApiHarness.Now.AddSeconds(-60).UtcDateTime);
    token.ValidTo.ShouldBe(GitHubApiHarness.Now.AddMinutes(9).UtcDateTime);
    request.Headers["X-GitHub-Api-Version"].ShouldBe(["2022-11-28"]);
    request.Headers["User-Agent"].ShouldHaveSingleItem().ShouldStartWith("CodeCoverage");
    request.Headers["Accept"].ShouldBe(["application/vnd.github+json"]);
  }

  [Fact]
  public async Task GetInstallationToken_AfterPemRotation_ShouldUseNewKey()
  {
    GivenToken(1, "ghs_before", TimeSpan.FromHours(1));
    GivenToken(2, "ghs_after", TimeSpan.FromHours(1));
    using var rotated = RSA.Create(2048);

    await _github.TokenProvider.GetInstallationTokenAsync(1, Cancellation);
    _github.UsePrivateKey(rotated);
    await _github.TokenProvider.GetInstallationTokenAsync(2, Cancellation);

    var jwts = _github.Server.LogEntries.Select(static e => BearerOf(e.RequestMessage!.Headers!["Authorization"][0])).ToList();

    GitHubApiHarness.IsSignedBy(jwts[0], _key).ShouldBeTrue();
    GitHubApiHarness.IsSignedBy(jwts[1], rotated).ShouldBeTrue();
    GitHubApiHarness.IsSignedBy(jwts[1], _key).ShouldBeFalse();
  }

  [Fact]
  public async Task GetInstallationToken_Refused_ShouldThrowBadGateway()
  {
    _github.Server.Given(TokenRequest(Installation)).RespondWith(Response.Create().WithStatusCode(401));

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _github.TokenProvider.GetInstallationTokenAsync(Installation, Cancellation)
    );

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
    _github.Server.LogEntries.Count().ShouldBe(1);
  }

  [Fact]
  public async Task GetInstallationToken_UnreadablePayload_ShouldThrowBadGateway()
  {
    _github.Server.Given(TokenRequest(Installation)).RespondWith(Response.Create().WithStatusCode(201).WithBody("<html>"));

    var exception = await Should.ThrowAsync<BadGatewayException>(
      () => _github.TokenProvider.GetInstallationTokenAsync(Installation, Cancellation)
    );

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
  }

  [Fact]
  public void CreateAppJwt_WithoutPrivateKey_ShouldThrowInvalidOperation()
  {
    _github.Options.PrivateKeyPem = null;

    Should.Throw<InvalidOperationException>(() => _github.TokenProvider.CreateAppJwt());
  }

  [Fact]
  public void CreateAppJwt_WithoutClientId_ShouldThrowInvalidOperation()
  {
    _github.Options.ClientId = null;

    Should.Throw<InvalidOperationException>(() => _github.TokenProvider.CreateAppJwt());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task GetInstallationToken_InvalidInstallation_ShouldThrowArgumentOutOfRange(long installation)
  {
    await Should.ThrowAsync<ArgumentOutOfRangeException>(
      () => _github.TokenProvider.GetInstallationTokenAsync(installation, Cancellation)
    );
  }

  private static string BearerOf(string authorization)
  {
    authorization.ShouldStartWith("Bearer ");
    return authorization["Bearer ".Length..];
  }

  private static IRequestBuilder TokenRequest(long installation) =>
    Request.Create().WithPath($"/app/installations/{installation}/access_tokens").UsingPost();

  private void GivenToken(long installation, string token, TimeSpan lifetime, TimeSpan? delay = null)
  {
    var expiresAt = DateTimeOffset.UtcNow.Add(lifetime).ToString("O", CultureInfo.InvariantCulture);
    var response = Response.Create().WithStatusCode(201).WithBody($$"""{"token":"{{token}}","expires_at":"{{expiresAt}}"}""");

    _github.Server.Given(TokenRequest(installation)).RespondWith(delay is { } wait ? response.WithDelay(wait) : response);
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _github.Dispose();
    _key.Dispose();
  }

  #endregion
}
