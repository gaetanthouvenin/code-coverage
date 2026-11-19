// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsUserTokenProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.AzureDevOps;
using CodeCoverage.Infrastructure.AzureDevOps.Models;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

// The tokens of a user live on its row, played here by one AppUser behind a fake repository; Entra ID is a stub that
// counts how many times it is asked and keeps each form it received.
public sealed class AzureDevOpsUserTokenProviderTests
{
  #region Fields

  private const string Prefix = "protected:";

  private const string Issued = """{"token_type":"Bearer","scope":"499b84ac-1321-427f-aa17-267ca6975798/user_impersonation","expires_in":4199,"ext_expires_in":4199,"access_token":"eyJ.new","refresh_token":"rt.new"}""";

  private const string Refused = """{"error":"invalid_grant","error_description":"AADSTS700082: The refresh token has expired due to inactivity.","error_codes":[700082]}""";

  private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly List<Uri> _requests = [];

  private AppUser _user = AppUser.Create(Forge.AzureDevOps, "6c1e8a0b-3f4d-4e5a-9b7c-2d1e0f9a8b7c", "Ada Lovelace", null);

  #endregion

  #region Ctors

  public AzureDevOpsUserTokenProviderTests()
  {
    A.CallTo(() => _users.FindAsync(A<long>._, A<CancellationToken>._)).ReturnsLazily(() => _user);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetTokenAsync_Expired_ShouldRefresh()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    var entra = Entra(static () => StubHttpMessageHandler.Json(Issued));
    using var services = Services(entra);

    (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe("eyJ.new");

    _user.AccessTokenProtected.ShouldBe(Prefix + "eyJ.new");
    _user.AccessTokenExpiresAt.ShouldBe(Now.AddSeconds(4199));
    _user.RefreshTokenProtected.ShouldBe(Prefix + "rt.new");
    A.CallTo(() => _users.UpdateAsync(_user, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    var body = Uri.UnescapeDataString(entra.Bodies.ShouldHaveSingleItem()).Replace('+', ' ');
    body.ShouldContain("grant_type=refresh_token");
    body.ShouldContain("refresh_token=rt.old");
    body.ShouldContain($"client_id={AzureDevOpsApiHarness.ClientId}");
    body.ShouldContain("client_secret=secret-1");
    body.ShouldContain("scope=499b84ac-1321-427f-aa17-267ca6975798/.default offline_access");
    _requests.ShouldHaveSingleItem().ShouldBe(new Uri("https://login.test/organizations/oauth2/v2.0/token"));
  }

  // Entra ID may keep the refresh token it was sent: the stored one stays.
  [Fact]
  public async Task GetTokenAsync_RenewedWithoutANewRefreshToken_ShouldKeepTheOldOne()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json("""{"access_token":"eyJ.new","expires_in":3600}""")));

    (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe("eyJ.new");

    _user.RefreshTokenProtected.ShouldBe(Prefix + "rt.old");
  }

  [Fact]
  public async Task GetTokenAsync_NotExpired_ShouldGiveTheStoredTokenWithoutAskingEntra()
  {
    _user.ReplaceTokens(Protector(), "eyJ.current", Now.AddHours(1), "rt.current", null);
    var entra = Entra(static () => StubHttpMessageHandler.Json(Issued));
    using var services = Services(entra);

    (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe("eyJ.current");

    entra.Calls.ShouldBe(0);
  }

  [Fact]
  public async Task GetTokenAsync_RefreshTokenRefused_ShouldClearTheTokensAndThrowUnauthenticated()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.dead", null);
    var entra = Entra(static () => StubHttpMessageHandler.Json(Refused, HttpStatusCode.BadRequest));
    using var services = Services(entra);

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(1, Ct));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    _user.AccessTokenProtected.ShouldBeNull();
    _user.RefreshTokenProtected.ShouldBeNull();
    entra.Calls.ShouldBe(1);
  }

  [Fact]
  public async Task GetTokenAsync_NoRefreshToken_ShouldThrowUnauthenticatedWithoutAskingEntra()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), null, null);
    var entra = Entra(static () => StubHttpMessageHandler.Json(Issued));
    using var services = Services(entra);

    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);

    entra.Calls.ShouldBe(0);
    _user.AccessTokenProtected.ShouldBeNull();
  }

  // A failure of Entra ID, or a refusal that is about the application, signs nobody out: the tokens stay.
  [Theory]
  [InlineData(400, """{"error":"invalid_client"}""")]
  [InlineData(401, "{}")]
  [InlineData(500, "{}")]
  [InlineData(200, "not json")]
  [InlineData(200, "{}")]
  public async Task GetTokenAsync_EntraFailsForAReasonThatIsNotAboutTheUser_ShouldThrowBadGatewayAndKeepTheTokens(int status, string body)
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    using var services = Services(Entra(() => StubHttpMessageHandler.Json(body, (HttpStatusCode)status)));

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Provider(services).GetTokenAsync(1, Ct));

    exception.ErrorCode.ShouldBe(ForgeErrors.Unavailable);
    _user.RefreshTokenProtected.ShouldBe(Prefix + "rt.old");
    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetTokenAsync_UnknownUser_ShouldThrowUnauthenticated()
  {
    _user = null!;
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json(Issued)));

    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(999, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Provider(services).GetTokenAsync(0, Ct));
  }

  // Without an expiry, the stored token is given as long as it is there.
  [Fact]
  public async Task GetTokenAsync_StoredWithoutExpiry_ShouldGiveTheStoredTokenWithoutAskingEntra()
  {
    _user.ReplaceTokens(Protector(), "eyJ.current", null, "rt.current", null);
    var entra = Entra(static () => StubHttpMessageHandler.Json(Issued));
    using var services = Services(entra);

    (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe("eyJ.current");

    entra.Calls.ShouldBe(0);
  }

  // The caller that held the lock renewed the token meanwhile: the reading under the lock gives it.
  [Fact]
  public async Task GetTokenAsync_RenewedMeanwhile_ShouldGiveTheRenewedTokenWithoutAskingEntra()
  {
    A.CallTo(() => _users.FindAsync(A<long>._, A<CancellationToken>._))
     .ReturnsNextFromSequence(User("eyJ.old", Now.AddMinutes(-1), "rt.old"), User("eyJ.renewed", Now.AddHours(1), "rt.renewed"));
    var entra = Entra(static () => StubHttpMessageHandler.Json(Issued));
    using var services = Services(entra);

    (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe("eyJ.renewed");

    entra.Calls.ShouldBe(0);
  }

  [Fact]
  public async Task GetTokenAsync_RefreshTokenExpired_ShouldClearTheTokensWithoutAskingEntra()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", Now.AddMinutes(-1));
    var entra = Entra(static () => StubHttpMessageHandler.Json(Issued));
    using var services = Services(entra);

    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);

    entra.Calls.ShouldBe(0);
    _user.RefreshTokenProtected.ShouldBeNull();
  }

  // Entra ID unreachable, or out of time while the caller still waits, signs nobody out.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task GetTokenAsync_EntraDoesNotAnswer_ShouldThrowBadGatewayAndKeepTheTokens(bool timedOut)
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    using var services = Services(
      Entra(() => timedOut ? throw new TaskCanceledException("The request timed out.") : throw new HttpRequestException("No route to host."))
    );

    (await Should.ThrowAsync<BadGatewayException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(ForgeErrors.Unavailable);

    _user.RefreshTokenProtected.ShouldBe(Prefix + "rt.old");
  }

  [Fact]
  public async Task GetTokenAsync_RenewedWithoutLifetime_ShouldStoreTheTokenWithoutExpiry()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json("""{"access_token":"eyJ.new","refresh_token":"rt.new"}""")));

    (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe("eyJ.new");

    _user.AccessTokenExpiresAt.ShouldBeNull();
  }

  // Another save won: the renewed pair is written again on what it wrote.
  [Fact]
  public async Task GetTokenAsync_RenewedPairSavedConcurrently_ShouldSaveItAgain()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).Throws(Concurrent()).Once();
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json(Issued)));

    (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe("eyJ.new");

    A.CallTo(() => _users.UpdateAsync(_user, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public async Task GetTokenAsync_RenewedPairAlwaysSavedConcurrently_ShouldThrowConflictAfterThreeAttempts()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).Throws(Concurrent());
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json(Issued)));

    (await Should.ThrowAsync<ConflictException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);

    A.CallTo(() => _users.UpdateAsync(_user, A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
  }

  // A conflict that is not a concurrent save is not tried again.
  [Fact]
  public async Task GetTokenAsync_RenewedPairInOtherConflict_ShouldThrowItWithoutTryingAgain()
  {
    _user.ReplaceTokens(Protector(), "eyJ.old", Now.AddMinutes(-1), "rt.old", null);
    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).Throws(new ConflictException(GeneralErrors.Unexpected, "Another conflict."));
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json(Issued)));

    (await Should.ThrowAsync<ConflictException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(GeneralErrors.Unexpected);

    A.CallTo(() => _users.UpdateAsync(_user, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A user who never signed in through Azure DevOps has nothing to renew.
  [Fact]
  public async Task GetTokenAsync_NoTokens_ShouldThrowUnauthenticatedWithoutAskingEntra()
  {
    var entra = Entra(static () => StubHttpMessageHandler.Json(Issued));
    using var services = Services(entra);

    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);

    entra.Calls.ShouldBe(0);
  }

  [Fact]
  public async Task GetTokenAsync_UserRemovedBeforeTheClearing_ShouldThrowUnauthenticated()
  {
    var expired = User("eyJ.old", Now.AddMinutes(-1), null);
    A.CallTo(() => _users.FindAsync(A<long>._, A<CancellationToken>._)).ReturnsNextFromSequence(expired, expired, null);
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json(Issued)));

    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);

    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // A sign-in stored a pair after the last reading: it is kept, and given while it lasts.
  [Theory]
  [InlineData(60, "eyJ.signin")]
  [InlineData(-1, null)]
  public async Task GetTokenAsync_PairStoredBeforeTheClearing_ShouldKeepIt(int lifetimeMinutes, string? expected)
  {
    var expired = User("eyJ.old", Now.AddMinutes(-1), null);
    A.CallTo(() => _users.FindAsync(A<long>._, A<CancellationToken>._))
     .ReturnsNextFromSequence(expired, expired, User("eyJ.signin", Now.AddMinutes(lifetimeMinutes), "rt.signin"));
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json(Issued)));

    await ShouldGiveAsync(services, expected);

    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The row changed while it was cleared: what was stored meanwhile is kept, and given while it lasts.
  [Theory]
  [InlineData(60, "eyJ.signin")]
  [InlineData(-1, null)]
  public async Task GetTokenAsync_ClearingSavedConcurrently_ShouldGiveThePairStoredMeanwhile(int lifetimeMinutes, string? expected)
  {
    var expired = User("eyJ.old", Now.AddMinutes(-1), null);
    A.CallTo(() => _users.FindAsync(A<long>._, A<CancellationToken>._))
     .ReturnsNextFromSequence(expired, expired, expired, User("eyJ.signin", Now.AddMinutes(lifetimeMinutes), "rt.signin"));
    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).Throws(Concurrent());
    using var services = Services(Entra(static () => StubHttpMessageHandler.Json(Issued)));

    await ShouldGiveAsync(services, expected);

    A.CallTo(() => _users.UpdateAsync(expired, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // Logged by mistake, the answer of Entra ID does not show the tokens.
  [Fact]
  public void TokenResponse_ToString_ShouldNotShowTheTokens()
  {
    var text = new EntraUserTokenResponse("eyJ.secret_access", 4199, "rt.secret_refresh").ToString();

    text.ShouldNotContain("eyJ.secret_access");
    text.ShouldNotContain("rt.secret_refresh");
    text.ShouldContain("4199");
  }

  [Fact]
  public void TokenResponse_ToStringWithoutTokens_ShouldMaskNothing()
  {
    var text = new EntraUserTokenResponse(Error: "invalid_grant").ToString();

    text.ShouldNotContain("***");
    text.ShouldContain("invalid_grant");
  }

  [Fact]
  public void Registration_ShouldResolveTheProviderOfAzureDevOps()
  {
    var services = new ServiceCollection();
    services.AddSingleton(A.Fake<IServiceScopeFactory>())
            .AddSingleton(A.Fake<IHttpClientFactory>())
            .AddSingleton(A.Fake<IOptionsMonitor<AzureDevOpsOptions>>())
            .AddSingleton(A.Fake<ISecretProtector>())
            .AddSingleton(A.Fake<IClock>())
            .AddKeyedSingleton<IForgeUserTokenProvider, AzureDevOpsUserTokenProvider>(Forge.AzureDevOps);
    using var provider = services.BuildServiceProvider(validateScopes: true);

    new ForgeGatewayProvider(provider).GetUserTokenProvider(Forge.AzureDevOps).ShouldBeOfType<AzureDevOpsUserTokenProvider>();
  }

  private static ConflictException Concurrent() => new(GeneralErrors.ConcurrentChange, "Another save won.");

  private StubHttpMessageHandler Entra(Func<HttpResponseMessage> respond) =>
    new(
      (request, _) =>
      {
        _requests.Add(request.RequestUri!);

        return Task.FromResult(respond());
      }
    );

  private static IForgeUserTokenProvider Provider(ServiceProvider services) => services.GetRequiredKeyedService<IForgeUserTokenProvider>(Forge.AzureDevOps);

  private static ISecretProtector Protector()
  {
    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => Prefix + value);
    A.CallTo(() => protector.Unprotect(A<string>._)).ReturnsLazily((string value) => value[Prefix.Length..]);

    return protector;
  }

  private static async Task ShouldGiveAsync(ServiceProvider services, string? expected)
  {
    if (expected is null)
    {
      (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(1, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    }
    else
    {
      (await Provider(services).GetTokenAsync(1, Ct)).ShouldBe(expected);
    }
  }

  private static AppUser User(string accessToken, DateTimeOffset accessTokenExpiresAt, string? refreshToken)
  {
    var user = AppUser.Create(Forge.AzureDevOps, "6c1e8a0b-3f4d-4e5a-9b7c-2d1e0f9a8b7c", "Ada Lovelace", null);
    user.ReplaceTokens(Protector(), accessToken, accessTokenExpiresAt, refreshToken, null);

    return user;
  }

  private ServiceProvider Services(StubHttpMessageHandler entra)
  {
    var options = A.Fake<IOptionsMonitor<AzureDevOpsOptions>>();
    A.CallTo(() => options.CurrentValue)
     .Returns(new AzureDevOpsOptions { Authority = "https://login.test/", ClientId = AzureDevOpsApiHarness.ClientId, ClientSecret = Prefix + "secret-1" });

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var factory = A.Fake<IHttpClientFactory>();
    A.CallTo(() => factory.CreateClient(AzureDevOpsHttpClientConfiguration.EntraClientName))
     .ReturnsLazily(() => new HttpClient(entra, disposeHandler: false));

    return new ServiceCollection()
           .AddScoped(_ => _users)
           .AddSingleton(Protector())
           .AddSingleton(options)
           .AddSingleton(clock)
           .AddSingleton(factory)
           .AddKeyedSingleton<IForgeUserTokenProvider, AzureDevOpsUserTokenProvider>(Forge.AzureDevOps)
           .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
  }

  #endregion
}
