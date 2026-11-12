// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenAuthenticationHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Claims;
using System.Text.Encodings.Web;

using Mediator;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Api.Configuration.Authentication;
using CodeCoverage.Api.Configuration.Identity;
using CodeCoverage.Application.Features.Access.Queries.AuthenticateApiToken;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Api.Tests.Authentication;

public sealed class ApiTokenAuthenticationHandlerTests
{
  #region Fields

  private const string Value = "cvp_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private readonly ISender _sender = A.Fake<ISender>();

  private AuthenticateApiTokenQuery? _sent;

  #endregion

  #region Ctors

  public ApiTokenAuthenticationHandlerTests()
  {
    A.CallTo(() => _sender.Send(A<IQuery<ApiTokenIdentity?>>._, A<CancellationToken>._))
     .ReturnsLazily((IQuery<ApiTokenIdentity?> query, CancellationToken _) =>
     {
       _sent = (AuthenticateApiTokenQuery)query;
       return ValueTask.FromResult<ApiTokenIdentity?>(null);
     });
  }

  #endregion

  #region Methods

  // The query answers nothing for a revoked token: the request fails, and its challenge is a 401 with a Bearer challenge.
  [Fact]
  public async Task Authenticate_RevokedToken_ShouldReturn401()
  {
    var (handler, http) = await CreateAsync($"Bearer {Value}");

    var result = await handler.AuthenticateAsync();
    await handler.ChallengeAsync(new AuthenticationProperties());

    result.Succeeded.ShouldBeFalse();
    result.Failure.ShouldNotBeNull().Message.ShouldNotContain(Value);
    http.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    http.Response.Headers.WWWAuthenticate.ToString().ShouldBe("Bearer");
  }

  // The principal names the user of the token: the access checks then run with the GitHub token stored for that user,
  // never with anything the caller sends.
  [Fact]
  public async Task Authenticate_Valid_ShouldUseStoredGitHubUserToken()
  {
    Returns(new ApiTokenIdentity(5, 42, "octocat", Forge.GitHub, "583231", 9));
    var (handler, http) = await CreateAsync($"Bearer {Value}");

    var result = await handler.AuthenticateAsync();

    result.Succeeded.ShouldBeTrue();
    _sent.ShouldNotBeNull().Value.ShouldBe(Value);
    var principal = result.Principal.ShouldNotBeNull();
    principal.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe("42");
    principal.Claims.ShouldNotContain(c => c.Value == Value);
    principal.Claims.ShouldNotContain(c => c.Type == ClaimTypes.Role);

    // The scope then runs for that user, bound to the organization of the token.
    http.User = principal;
    var currentUser = new CurrentUserContext();
    await new HttpCurrentUserMiddleware(static _ => Task.CompletedTask).InvokeAsync(http, currentUser);

    currentUser.UserId.ShouldBe(42);
    currentUser.TokenOrganizationId.ShouldBe(9);
    currentUser.IsInstanceToken.ShouldBeFalse();
  }

  [Fact]
  public async Task Authenticate_TokenOfTheInstance_ShouldEnterNoOrganization()
  {
    Returns(new ApiTokenIdentity(5, 42, "octocat", Forge.GitHub, "583231", null));
    var (handler, http) = await CreateAsync($"Bearer {Value}");

    http.User = (await handler.AuthenticateAsync()).Principal!;
    var currentUser = new CurrentUserContext();
    await new HttpCurrentUserMiddleware(static _ => Task.CompletedTask).InvokeAsync(http, currentUser);

    currentUser.IsInstanceToken.ShouldBeTrue();
    currentUser.TokenOrganizationId.ShouldBeNull();
  }

  // The upload tokens and the imported Codecov tokens travel in the same header: they are left to the uploads.
  [Theory]
  [InlineData(null)]
  [InlineData("Bearer cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG")]
  [InlineData("Bearer 0a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d")]
  [InlineData("Basic Y3ZwXzpwYXNz")]
  [InlineData("Bearer CVP_upper")]
  public async Task Authenticate_NoPersonalToken_ShouldLeaveTheRequestAlone(string? header)
  {
    var (handler, _) = await CreateAsync(header);

    var result = await handler.AuthenticateAsync();

    result.None.ShouldBeTrue();
    A.CallTo(_sender).MustNotHaveHappened();
  }

  private void Returns(ApiTokenIdentity identity)
  {
    A.CallTo(() => _sender.Send(A<IQuery<ApiTokenIdentity?>>._, A<CancellationToken>._))
     .ReturnsLazily((IQuery<ApiTokenIdentity?> query, CancellationToken _) =>
     {
       _sent = (AuthenticateApiTokenQuery)query;
       return ValueTask.FromResult<ApiTokenIdentity?>(identity);
     });
  }

  private async Task<(ApiTokenAuthenticationHandler Handler, DefaultHttpContext Http)> CreateAsync(string? header)
  {
    var options = A.Fake<IOptionsMonitor<AuthenticationSchemeOptions>>();
    A.CallTo(() => options.Get(A<string>._)).Returns(new AuthenticationSchemeOptions());

    var handler = new ApiTokenAuthenticationHandler(options, NullLoggerFactory.Instance, UrlEncoder.Default, _sender);
    var http = new DefaultHttpContext();

    if (header is not null)
    {
      http.Request.Headers.Authorization = header;
    }

    await handler.InitializeAsync(new AuthenticationScheme(ApiTokenAuthenticationHandler.SchemeName, null, typeof(ApiTokenAuthenticationHandler)), http);

    return (handler, http);
  }

  #endregion
}
