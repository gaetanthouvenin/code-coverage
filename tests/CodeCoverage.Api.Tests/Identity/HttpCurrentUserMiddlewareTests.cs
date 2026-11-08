// ------------------------------------------------------------------------------------------------
// <copyright file="HttpCurrentUserMiddlewareTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using CodeCoverage.Api.Configuration.Identity;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Api.Tests.Identity;

public sealed class HttpCurrentUserMiddlewareTests
{
  #region Methods

  [Fact]
  public async Task Invoke_Anonymous_ShouldLeaveTheScopeAnonymous()
  {
    var (currentUser, called) = await InvokeAsync(new ClaimsPrincipal(new ClaimsIdentity()));

    currentUser.IsAuthenticated.ShouldBeFalse();
    currentUser.IsSystem.ShouldBeFalse();
    called.ShouldBeTrue();
  }

  [Fact]
  public async Task Invoke_AuthenticatedAdmin_ShouldSetTheUser()
  {
    var principal = Authenticated(
      new Claim(ClaimTypes.NameIdentifier, "42"),
      new Claim(ClaimTypes.Name, "octocat"),
      new Claim(ClaimTypes.Role, HttpCurrentUserMiddleware.AdminRole));

    var (currentUser, called) = await InvokeAsync(principal);

    currentUser.UserId.ShouldBe(42);
    currentUser.Login.ShouldBe("octocat");
    currentUser.IsAdmin.ShouldBeTrue();
    currentUser.IsSystem.ShouldBeFalse();
    called.ShouldBeTrue();
  }

  [Fact]
  public async Task Invoke_AuthenticatedWithoutNumericIdentifier_ShouldLeaveTheScopeAnonymous()
  {
    var principal = Authenticated(new Claim(ClaimTypes.NameIdentifier, "octocat"), new Claim(ClaimTypes.Name, "octocat"));

    var (currentUser, _) = await InvokeAsync(principal);

    currentUser.IsAuthenticated.ShouldBeFalse();
  }

  private static ClaimsPrincipal Authenticated(params Claim[] claims) => new(new ClaimsIdentity(claims, "Test"));

  private static async Task<(CurrentUserContext CurrentUser, bool Called)> InvokeAsync(ClaimsPrincipal principal)
  {
    var called = false;
    var middleware = new HttpCurrentUserMiddleware(_ =>
                                                   {
                                                     called = true;
                                                     return Task.CompletedTask;
                                                   });
    var currentUser = new CurrentUserContext();

    await middleware.InvokeAsync(new DefaultHttpContext { User = principal }, currentUser);

    return (currentUser, called);
  }

  #endregion
}
