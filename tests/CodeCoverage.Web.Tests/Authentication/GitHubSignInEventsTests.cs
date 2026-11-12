// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubSignInEventsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Claims;
using System.Text.Json;

using AspNet.Security.OAuth.GitHub;

using Mediator;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Access.Commands.SignInUser;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Web.Configuration.Authentication;

namespace CodeCoverage.Web.Tests.Authentication;

public sealed class GitHubSignInEventsTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly AuthenticationScheme Scheme = new(GitHubAuthenticationDefaults.AuthenticationScheme, null, typeof(GitHubAuthenticationHandler));

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly GitHubSignInEvents _events;

  private SignInUserCommand? _sent;

  #endregion

  #region Ctors

  public GitHubSignInEventsTests()
  {
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _sender.Send(A<SignInUserCommand>._, A<CancellationToken>._))
     .ReturnsLazily((ICommand<long> command, CancellationToken _) =>
     {
       _sent = (SignInUserCommand)command;
       return ValueTask.FromResult(42L);
     });

    _events = new GitHubSignInEvents(_sender, clock, NullLogger<GitHubSignInEvents>.Instance);
  }

  #endregion

  #region Methods

  [Fact]
  public async Task CreatingTicket_ShouldSignInWithTheTokensAndKeepOnlyTheSession()
  {
    using var tokens = JsonDocument.Parse("""{"access_token":"ghu_a","refresh_token":"ghr_r","expires_in":"28800","refresh_token_expires_in":15897600}""");
    var context = TicketContext(tokens);

    await _events.CreatingTicket(context);

    var command = _sent.ShouldNotBeNull();
    command.AccessToken.ShouldBe("ghu_a");
    command.AccessTokenLifetime.ShouldBe(TimeSpan.FromHours(8));
    command.RefreshToken.ShouldBe("ghr_r");
    command.RefreshTokenLifetime.ShouldBe(TimeSpan.FromSeconds(15897600));
    // The login and the other claims of GitHub are dropped: the cookie keeps the session only.
    context.Principal.ShouldNotBeNull().Claims.Select(static c => c.Type).ShouldBe([SessionPrincipal.UserIdClaim, SessionPrincipal.SessionClaim]);
    SessionPrincipal.TryReadUserId(context.Principal, out var userId).ShouldBeTrue();
    userId.ShouldBe(42);
  }

  [Fact]
  public async Task CreatingTicket_Refused_ShouldThrowSoThatNoTicketIsIssued()
  {
    using var tokens = JsonDocument.Parse("""{"access_token":"ghu_a"}""");
    A.CallTo(() => _sender.Send(A<SignInUserCommand>._, A<CancellationToken>._)).Throws(new ForbiddenException(AccessErrors.NoRepository, "No repository."));

    await Should.ThrowAsync<ForbiddenException>(() => _events.CreatingTicket(TicketContext(tokens)));
  }

  [Theory]
  [InlineData(true, GitHubSignInEvents.NoRepositoryPath)]
  [InlineData(false, GitHubSignInEvents.FailedPath)]
  public async Task RemoteFailure_ShouldRedirectWithoutAnySession(bool noRepository, string expected)
  {
    var http = new DefaultHttpContext();
    Exception failure = noRepository ? new ForbiddenException(AccessErrors.NoRepository, "No repository.") : new InvalidOperationException("ghu_secret");
    var context = new RemoteFailureContext(http, Scheme, new GitHubAuthenticationOptions(), failure);

    await _events.RemoteFailure(context);

    http.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
    http.Response.Headers.Location.ToString().ShouldBe(expected);
    context.Result.ShouldNotBeNull().Handled.ShouldBeTrue();
  }

  [Theory]
  [InlineData("""{"refresh_token_expires_in":15897600}""", 15897600)]
  [InlineData("""{"refresh_token_expires_in":"15897600"}""", 15897600)]
  [InlineData("""{"refresh_token_expires_in":0}""", null)]
  [InlineData("""{"refresh_token_expires_in":"soon"}""", null)]
  [InlineData("""{"other":1}""", null)]
  public void ReadLifetime_ShouldReadNumbersAndNumericStrings(string json, int? expected)
  {
    using var document = JsonDocument.Parse(json);

    GitHubSignInEvents.ReadLifetime(document, "refresh_token_expires_in").ShouldBe(expected is null ? null : TimeSpan.FromSeconds(expected.Value));
  }

  private static OAuthCreatingTicketContext TicketContext(JsonDocument tokens)
  {
    using var user = JsonDocument.Parse("""{"id":583231,"login":"octocat"}""");
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "octocat")], "GitHub"));

    return new OAuthCreatingTicketContext(
      principal,
      new AuthenticationProperties(),
      new DefaultHttpContext(),
      Scheme,
      new GitHubAuthenticationOptions(),
      new HttpClient(),
      OAuthTokenResponse.Success(tokens),
      user.RootElement.Clone()
    );
  }

  #endregion
}
