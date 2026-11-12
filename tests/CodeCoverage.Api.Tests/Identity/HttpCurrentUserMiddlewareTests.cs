// ------------------------------------------------------------------------------------------------
// <copyright file="HttpCurrentUserMiddlewareTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using CodeCoverage.Domain.Organizations;
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
  public async Task Invoke_Authenticated_ShouldSetTheUser()
  {
    var principal = Authenticated(
      new Claim(ClaimTypes.NameIdentifier, "42"),
      new Claim(ClaimTypes.Name, "octocat"),
      new Claim(HttpCurrentUserMiddleware.ForgeClaim, "GitHub"),
      new Claim(HttpCurrentUserMiddleware.ExternalIdClaim, "583231"),
      new Claim(ClaimTypes.Role, "Admin"));

    var (currentUser, called) = await InvokeAsync(principal);

    currentUser.UserId.ShouldBe(42);
    currentUser.Login.ShouldBe("octocat");
    currentUser.Forge.ShouldBe(Forge.GitHub);
    currentUser.ExternalId.ShouldBe("583231");
    // No claim makes an administrator, a role included.
    currentUser.IsAdmin.ShouldBeFalse();
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

  [Theory]
  [InlineData(null, "583231")]
  [InlineData("github", "583231")]
  [InlineData("99", "583231")]
  [InlineData("GitHub", null)]
  [InlineData("GitHub", " ")]
  public async Task Invoke_AuthenticatedWithoutAccount_ShouldLeaveTheScopeAnonymous(string? forge, string? externalId)
  {
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "42"), new(ClaimTypes.Name, "octocat") };

    if (forge is not null)
    {
      claims.Add(new Claim(HttpCurrentUserMiddleware.ForgeClaim, forge));
    }

    if (externalId is not null)
    {
      claims.Add(new Claim(HttpCurrentUserMiddleware.ExternalIdClaim, externalId));
    }

    var (currentUser, _) = await InvokeAsync(Authenticated([.. claims]));

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
