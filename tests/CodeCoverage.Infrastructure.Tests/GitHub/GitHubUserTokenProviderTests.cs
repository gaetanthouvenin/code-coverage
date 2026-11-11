// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserTokenProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.GitHub.Models;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

// The tokens of a user live on its row, in the real database of both engines; only GitHub is faked, and it counts
// how many times it is asked: a refresh token is accepted once.
[Collection(DatabaseCollection.Name)]
public sealed class GitHubUserTokenProviderTests(DatabaseFixture database)
{
  #region Fields

  private const string Prefix = "protected:";

  private const string Issued = """{"access_token":"ghu_new","expires_in":28800,"refresh_token":"ghr_new","refresh_token_expires_in":15897600,"token_type":"bearer","scope":""}""";

  private const string Refused = """{"error":"bad_refresh_token","error_description":"The refresh token passed is incorrect or expired."}""";

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_NotExpired_ShouldGiveTheStoredTokenWithoutAskingGitHub(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_current", Now.AddHours(2), "ghr_current", Now.AddMonths(6));
    var github = GitHub(static () => StubHttpMessageHandler.Json(Issued));
    await using var services = Services(seed, github);

    (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe("ghu_current");

    github.Calls.ShouldBe(0);
  }

  // An App that does not expire its user tokens issues one without expiry and without a refresh token.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_TokenThatNeverExpires_ShouldGiveIt(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_forever", null, null, null);
    var github = GitHub(static () => StubHttpMessageHandler.Json(Issued));
    await using var services = Services(seed, github);

    (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe("ghu_forever");

    github.Calls.ShouldBe(0);
  }

  // The acceptance of M3.02: many callers ask at once for a user whose token expired. GitHub is asked once, the
  // refresh token is sent once, and every caller gets the new token. GitHub answers only once every caller has read
  // the expired token, so that all of them overlap the renewal. Ten rounds on each engine.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_Expired_ShouldRefreshOnceUnderConcurrency(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

    for (var round = 1; round <= 10; round++)
    {
      await SetTokensAsync(seed, userId, "ghu_old", Now.AddMinutes(-1), $"ghr_round_{round}", Now.AddMonths(6));

      var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var github = new StubHttpMessageHandler(
        async (_, _) =>
        {
          // GitHub answers only once every caller has had the time to ask: none of them may send a second request.
          await release.Task;

          return StubHttpMessageHandler.Json(Issued.Replace("ghu_new", $"ghu_round_{round}", StringComparison.Ordinal));
        }
      );
      // Never runs its action: it only counts the readings.
      var hook = new UserReadingHook(0, static () => Task.CompletedTask);
      await using var services = Services(seed, github, hook);
      var provider1 = Provider(services);

      var callers = Enumerable.Range(0, 16).Select(_ => Task.Run(() => provider1.GetTokenAsync(userId, Ct), Ct)).ToArray();
      // Sixteen first looks, all expired, and the reading of the caller that took the lock.
      await hook.ReachedAsync(17, Ct);
      release.SetResult();

      var tokens = await Task.WhenAll(callers);

      tokens.ShouldAllBe(token => token == $"ghu_round_{round}", $"round {round}");
      github.Calls.ShouldBe(1, $"round {round}: the refresh token was sent more than once");
      github.Bodies.ShouldHaveSingleItem().ShouldContain($"refresh_token=ghr_round_{round}");
    }
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_Expired_ShouldStoreTheNewPairProtectedWithItsExpiries(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));
    var github = GitHub(static () => StubHttpMessageHandler.Json(Issued));
    await using var services = Services(seed, github);

    (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe("ghu_new");

    var user = await ReadUserAsync(seed, userId);
    user.AccessTokenProtected.ShouldBe(Prefix + "ghu_new");
    user.RefreshTokenProtected.ShouldBe(Prefix + "ghr_new");
    user.AccessTokenExpiresAt.ShouldBe(Now.AddSeconds(28800));
    user.RefreshTokenExpiresAt.ShouldBe(Now.AddSeconds(15897600));

    var body = github.Bodies.ShouldHaveSingleItem();
    body.ShouldContain("grant_type=refresh_token");
    body.ShouldContain("refresh_token=ghr_old");
    body.ShouldContain("client_id=Iv1.test-client");
    body.ShouldContain("client_secret=the-client-secret");

    // The new token is given from the row from now on.
    (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe("ghu_new");
    github.Calls.ShouldBe(1);
  }

  // Renewed a little before it expires: a token is never given that dies during the call it serves.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_AboutToExpire_ShouldRefresh(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(4), "ghr_old", Now.AddMonths(6));
    var github = GitHub(static () => StubHttpMessageHandler.Json(Issued));
    await using var services = Services(seed, github);

    (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe("ghu_new");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_RefreshFails_ShouldThrowUnauthorized(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_dead", Now.AddMonths(6));
    var github = GitHub(static () => StubHttpMessageHandler.Json(Refused));
    await using var services = Services(seed, github);

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(userId, Ct));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    github.Calls.ShouldBe(1);

    // The dead pair is cleared: the next call does not send it again.
    var user = await ReadUserAsync(seed, userId);
    user.AccessTokenProtected.ShouldBeNull();
    user.RefreshTokenProtected.ShouldBeNull();

    await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(userId, Ct));
    github.Calls.ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_RefreshTokenExpiredOrMissing_ShouldThrowUnauthorizedWithoutAskingGitHub(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var expired = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMinutes(-1), externalId: "1");
    var missing = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), null, null, externalId: "2");
    var github = GitHub(static () => StubHttpMessageHandler.Json(Issued));
    await using var services = Services(seed, github);

    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(expired, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(missing, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);

    github.Calls.ShouldBe(0);
    (await ReadUserAsync(seed, expired)).AccessTokenProtected.ShouldBeNull();
    (await ReadUserAsync(seed, missing)).AccessTokenProtected.ShouldBeNull();
  }

  // Two processes renew at once: the Api and the Web do not share a lock. GitHub accepts the refresh token of one
  // and refuses the other, which must take the pair the first one stored instead of signing the user out. Forced:
  // the other process stores its pair while GitHub answers the refusal. Ten rounds on each engine.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_RefusedBecauseAnotherProcessRefreshedFirst_ShouldGiveThePairThatProcessStored(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

    for (var round = 1; round <= 10; round++)
    {
      await SetTokensAsync(seed, userId, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

      var github = new StubHttpMessageHandler(
        async (_, _) =>
        {
          await SetTokensAsync(seed, userId, $"ghu_winner_{round}", Now.AddHours(8), $"ghr_winner_{round}", Now.AddMonths(6));

          return StubHttpMessageHandler.Json(Refused);
        }
      );
      await using var services = Services(seed, github);

      (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe($"ghu_winner_{round}", $"round {round}");

      github.Calls.ShouldBe(1);
      (await ReadUserAsync(seed, userId)).RefreshTokenProtected.ShouldBe(Prefix + $"ghr_winner_{round}");
    }
  }

  // F8. The process that won stores its pair late: after the last reading of the loser, before it clears the row.
  // The loser must not erase the only refresh token that still works. Forced, ten rounds on each engine.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_WinnerStoresAfterTheLastReading_ShouldNotEraseItsPair(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

    for (var round = 1; round <= 10; round++)
    {
      await SetTokensAsync(seed, userId, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

      var winner = round;
      // The readings: the first look, the one under the lock, three after the refusal; the next one is the last.
      var hook = new UserReadingHook(6, () => SetTokensAsync(seed, userId, $"ghu_winner_{winner}", Now.AddHours(8), $"ghr_winner_{winner}", Now.AddMonths(6)));
      var github = GitHub(static () => StubHttpMessageHandler.Json(Refused));
      await using var services = Services(seed, github, hook);

      (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe($"ghu_winner_{round}", $"round {round}");

      hook.Readings.ShouldBeGreaterThanOrEqualTo(6, $"round {round}: the late winner was never reached");
      var stored = await ReadUserAsync(seed, userId);
      stored.RefreshTokenProtected.ShouldBe(Prefix + $"ghr_winner_{round}", $"round {round}");
      stored.AccessTokenProtected.ShouldBe(Prefix + $"ghu_winner_{round}", $"round {round}");
    }
  }

  // S5. The winner stores its pair between the reading of the clearing and its save: the save loses on the version,
  // and the loser gives the pair the winner stored instead of signing the user out. Forced, ten rounds on each engine.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_WinnerStoresBetweenTheReadingAndTheSaveOfTheClearing_ShouldGiveItsPair(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

    for (var round = 1; round <= 10; round++)
    {
      await SetTokensAsync(seed, userId, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

      var winner = round;
      var hook = new UserReadingHook(
        0,
        static () => Task.CompletedTask,
        () => SetTokensAsync(seed, userId, $"ghu_winner_{winner}", Now.AddHours(8), $"ghr_winner_{winner}", Now.AddMonths(6))
      );
      var github = GitHub(static () => StubHttpMessageHandler.Json(Refused));
      await using var services = Services(seed, github, hook);

      (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe($"ghu_winner_{round}", $"round {round}");

      (await ReadUserAsync(seed, userId)).RefreshTokenProtected.ShouldBe(Prefix + $"ghr_winner_{round}", $"round {round}");
    }
  }

  // F8. A refresh token found missing or expired: a sign-in that stored a new pair meanwhile is kept.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_SignInWhileTheExpiredPairIsCleared_ShouldKeepTheNewPair(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMinutes(-1));
    // The first look, the one under the lock; the next one is the reading of the clearing.
    var hook = new UserReadingHook(3, () => SetTokensAsync(seed, userId, "ghu_signed_in", Now.AddHours(8), "ghr_signed_in", Now.AddMonths(6)));
    var github = GitHub(static () => StubHttpMessageHandler.Json(Issued));
    await using var services = Services(seed, github, hook);

    (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe("ghu_signed_in");

    (await ReadUserAsync(seed, userId)).RefreshTokenProtected.ShouldBe(Prefix + "ghr_signed_in");
    github.Calls.ShouldBe(0);
  }

  // F9. GitHub rotated the pair: the caller that gives up while the answer comes back must not lose it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_CallerCancelsOnceGitHubAnswered_ShouldStillStoreTheNewPair(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));
    using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
    var github = new StubHttpMessageHandler(
      async (_, _) =>
      {
        // GitHub used the refresh token; the caller gives up before the answer is read.
        await caller.CancelAsync();

        return StubHttpMessageHandler.Json(Issued);
      }
    );
    await using var services = Services(seed, github);

    try
    {
      await Provider(services).GetTokenAsync(userId, caller.Token);
    }
    catch (OperationCanceledException)
    {
      // Giving up is the right of the caller; losing the pair is not.
    }

    var stored = await ReadUserAsync(seed, userId);
    stored.AccessTokenProtected.ShouldBe(Prefix + "ghu_new");
    stored.RefreshTokenProtected.ShouldBe(Prefix + "ghr_new");
    github.Calls.ShouldBe(1);
  }

  // F10. A request that hangs does not hold the lock of the user for the 100 seconds of the default timeout.
  [Fact]
  public void OAuthClient_ShouldHaveAShortTimeout()
  {
    using var services = new ServiceCollection().AddLogging().AddSingleton(A.Fake<IOptionsMonitor<GitHubOptions>>()).AddGitHubHttpClient().BuildServiceProvider();
    using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(GitHubHttpClientConfiguration.OAuthClientName);

    client.Timeout.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
  }

  // F10. Logged by mistake, the answer of GitHub does not show the tokens.
  [Fact]
  public void TokenResponse_ToString_ShouldNotShowTheTokens()
  {
    var text = new GitHubUserTokenResponse("ghu_secret_access", 28800, "ghr_secret_refresh", 15897600).ToString();

    text.ShouldNotContain("ghu_secret_access");
    text.ShouldNotContain("ghr_secret_refresh");
    text.ShouldContain("28800");
  }

  // GitHub invalidated the old pair as soon as it answered: the new one must reach the row, even when the row
  // changed while GitHub answered (a sign-in recorded by another request). Forced, ten rounds on each engine.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_RowChangedWhileGitHubAnswered_ShouldStillStoreTheNewPair(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

    for (var round = 1; round <= 10; round++)
    {
      await SetTokensAsync(seed, userId, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));

      var github = new StubHttpMessageHandler(
        async (_, _) =>
        {
          await using var other = seed.ContextWithoutOrganization();
          var user = await other.Users.SingleAsync(u => u.Id == userId, Ct);
          user.RecordSignIn($"renamed-{round}", null, Now);
          await other.SaveChangesAsync(Ct);

          return StubHttpMessageHandler.Json(Issued.Replace("ghu_new", $"ghu_round_{round}", StringComparison.Ordinal));
        }
      );
      await using var services = Services(seed, github);

      (await Provider(services).GetTokenAsync(userId, Ct)).ShouldBe($"ghu_round_{round}", $"round {round}");

      var stored = await ReadUserAsync(seed, userId);
      stored.AccessTokenProtected.ShouldBe(Prefix + $"ghu_round_{round}");
      stored.RefreshTokenProtected.ShouldBe(Prefix + "ghr_new");
      // The sign-in is kept too.
      stored.Login.ShouldBe($"renamed-{round}");
      github.Calls.ShouldBe(1);
    }
  }

  // A failure of GitHub, or a refusal that is about the App, signs nobody out: the tokens stay.
  [Theory]
  [MemberData(nameof(Failures))]
  public async Task GetToken_GitHubFailsForAReasonThatIsNotAboutTheUser_ShouldThrowBadGatewayAndKeepTheTokens(DatabaseProvider provider, int status, string body)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));
    var github = GitHub(() => StubHttpMessageHandler.Json(body, (HttpStatusCode)status));
    await using var services = Services(seed, github);

    var exception = await Should.ThrowAsync<BadGatewayException>(() => Provider(services).GetTokenAsync(userId, Ct));

    exception.ErrorCode.ShouldBe(GitHubErrors.Unavailable);
    (await ReadUserAsync(seed, userId)).RefreshTokenProtected.ShouldBe(Prefix + "ghr_old");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_GitHubUnreachable_ShouldThrowBadGatewayAndKeepTheTokens(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var userId = await AddUserAsync(seed, "ghu_old", Now.AddMinutes(-1), "ghr_old", Now.AddMonths(6));
    var github = new StubHttpMessageHandler(static (_, _) => throw new HttpRequestException("no route"));
    await using var services = Services(seed, github);

    await Should.ThrowAsync<BadGatewayException>(() => Provider(services).GetTokenAsync(userId, Ct));

    (await ReadUserAsync(seed, userId)).RefreshTokenProtected.ShouldBe(Prefix + "ghr_old");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetToken_UnknownUser_ShouldThrowUnauthorized(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var services = Services(seed, GitHub(static () => StubHttpMessageHandler.Json(Issued)));

    (await Should.ThrowAsync<UnauthorizedException>(() => Provider(services).GetTokenAsync(999, Ct))).ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Provider(services).GetTokenAsync(0, Ct));
  }

  public static TheoryData<DatabaseProvider, int, string> Failures()
  {
    var data = new TheoryData<DatabaseProvider, int, string>();

    foreach (var provider in new[] { DatabaseProvider.PostgreSql, DatabaseProvider.SqlServer })
    {
      data.Add(provider, 500, "{}");
      data.Add(provider, 404, "{}");
      data.Add(provider, 200, """{"error":"incorrect_client_credentials"}""");
      data.Add(provider, 200, "not json");
      data.Add(provider, 200, "{}");
    }

    return data;
  }

  private static StubHttpMessageHandler GitHub(Func<HttpResponseMessage> respond) => new((_, _) => Task.FromResult(respond()));

  private static IGitHubUserTokenProvider Provider(ServiceProvider services) => services.GetRequiredService<IGitHubUserTokenProvider>();

  private static ServiceProvider Services(TwoOrganizationDatabase seed, StubHttpMessageHandler github, UserReadingHook? hook = null)
  {
    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => Prefix + value);
    A.CallTo(() => protector.Unprotect(A<string>._)).ReturnsLazily((string value) => value[Prefix.Length..]);

    var options = A.Fake<IOptionsMonitor<GitHubOptions>>();
    A.CallTo(() => options.CurrentValue).Returns(new GitHubOptions { ClientId = "Iv1.test-client", ClientSecret = Prefix + "the-client-secret" });

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var factory = A.Fake<IHttpClientFactory>();
    A.CallTo(() => factory.CreateClient(GitHubHttpClientConfiguration.OAuthClientName))
     .ReturnsLazily(() => new HttpClient(github, disposeHandler: false) { BaseAddress = new Uri("https://github.test/") });

    // As the hosts do: one database context and one repository per scope, on a table that belongs to no organization.
    return new ServiceCollection()
           .AddScoped(_ => seed.ContextWithoutOrganization())
           .AddScoped<IUserRepository>(
             provider =>
             {
               var users = new UserRepository(provider.GetRequiredService<CodeCoverageDbContext>());

               return hook is null ? users : new HookedUserRepository(users, hook);
             }
           )
           .AddSingleton(protector)
           .AddSingleton(options)
           .AddSingleton(clock)
           .AddSingleton(factory)
           .AddSingleton<IGitHubUserTokenProvider, GitHubUserTokenProvider>()
           .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
  }

  private static async Task<long> AddUserAsync(
    TwoOrganizationDatabase seed,
    string accessToken,
    DateTimeOffset? accessExpiresAt,
    string? refreshToken,
    DateTimeOffset? refreshExpiresAt,
    string externalId = "583231")
  {
    await using var context = seed.ContextWithoutOrganization();
    var user = AppUser.Create(Forge.GitHub, externalId, "octocat", null);

    user.ReplaceTokens(Protector(), accessToken, accessExpiresAt, refreshToken, refreshExpiresAt);
    context.Users.Add(user);
    await context.SaveChangesAsync(Ct);

    return user.Id;
  }

  // What another process does: it writes the row through its own context.
  private static async Task SetTokensAsync(
    TwoOrganizationDatabase seed,
    long userId,
    string accessToken,
    DateTimeOffset? accessExpiresAt,
    string? refreshToken,
    DateTimeOffset? refreshExpiresAt)
  {
    await using var context = seed.ContextWithoutOrganization();
    var user = await context.Users.SingleAsync(u => u.Id == userId, Ct);

    user.ReplaceTokens(Protector(), accessToken, accessExpiresAt, refreshToken, refreshExpiresAt);
    await context.SaveChangesAsync(Ct);
  }

  private static async Task<AppUser> ReadUserAsync(TwoOrganizationDatabase seed, long userId)
  {
    await using var context = seed.ContextWithoutOrganization();

    return await context.Users.AsNoTracking().SingleAsync(u => u.Id == userId, Ct);
  }

  private static ISecretProtector Protector()
  {
    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => Prefix + value);

    return protector;
  }

  #endregion
}
