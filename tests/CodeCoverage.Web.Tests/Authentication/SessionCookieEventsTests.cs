// ------------------------------------------------------------------------------------------------
// <copyright file="SessionCookieEventsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Configuration.Authentication;

namespace CodeCoverage.Web.Tests.Authentication;

public sealed class SessionCookieEventsTests
{
  #region Fields

  private static readonly AuthenticationScheme Scheme = new(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly IAuthenticationService _authentication = A.Fake<IAuthenticationService>();

  private readonly CurrentUserContext _currentUser = new();

  private readonly SessionCookieEvents _events;

  #endregion

  #region Ctors

  public SessionCookieEventsTests()
  {
    _events = new SessionCookieEvents(_users, _currentUser);
  }

  #endregion

  #region Methods

  [Fact]
  public async Task ValidatePrincipal_UserWithTokens_ShouldSetThePrincipalOfTheRequest()
  {
    A.CallTo(() => _users.FindAsync(42, A<CancellationToken>._)).Returns(User(withTokens: true));
    var context = Context();

    await _events.ValidatePrincipal(context);

    context.Principal.ShouldNotBeNull();
    _currentUser.UserId.ShouldBe(42);
    _currentUser.Login.ShouldBe("octocat");
    _currentUser.IsAdmin.ShouldBeFalse();
  }

  // A renewal GitHub refused cleared the tokens: the session ends, and the next page asks to sign in.
  [Fact]
  public async Task ValidatePrincipal_UserWithoutTokens_ShouldRejectAndSignOut()
  {
    A.CallTo(() => _users.FindAsync(42, A<CancellationToken>._)).Returns(User(withTokens: false));
    var context = Context();

    await _events.ValidatePrincipal(context);

    context.Principal.ShouldBeNull();
    _currentUser.IsAuthenticated.ShouldBeFalse();
    A.CallTo(() => _authentication.SignOutAsync(A<HttpContext>._, CookieAuthenticationDefaults.AuthenticationScheme, A<AuthenticationProperties?>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task ValidatePrincipal_UserRemoved_ShouldReject()
  {
    A.CallTo(() => _users.FindAsync(42, A<CancellationToken>._)).Returns((AppUser?)null);
    var context = Context();

    await _events.ValidatePrincipal(context);

    context.Principal.ShouldBeNull();
  }

  private CookieValidatePrincipalContext Context()
  {
    var http = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(_authentication).BuildServiceProvider() };
    var ticket = new AuthenticationTicket(SessionPrincipal.Create(42, DateTimeOffset.UnixEpoch), Scheme.Name);

    return new CookieValidatePrincipalContext(http, Scheme, new CookieAuthenticationOptions(), ticket);
  }

  private static AppUser User(bool withTokens)
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, 42L);

    if (withTokens)
    {
      var protector = A.Fake<ISecretProtector>();
      A.CallTo(() => protector.Protect(A<string>._)).Returns("protected");
      user.ReplaceTokens(protector, "ghu_a", null, null, null);
    }

    return user;
  }

  #endregion
}
