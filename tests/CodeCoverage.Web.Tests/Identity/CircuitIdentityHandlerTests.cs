// ------------------------------------------------------------------------------------------------
// <copyright file="CircuitIdentityHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Configuration.Identity;

namespace CodeCoverage.Web.Tests.Identity;

public sealed class CircuitIdentityHandlerTests : IDisposable
{
  #region Fields

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly AuthenticationStateProvider _authentication = A.Fake<AuthenticationStateProvider>();

  private readonly CurrentUserContext _circuitUser = new();

  private readonly ServiceProvider _services;

  private readonly CircuitIdentityHandler _handler;

  #endregion

  #region Ctors

  public CircuitIdentityHandlerTests()
  {
    _services = new ServiceCollection().AddScoped(_ => _users).BuildServiceProvider();
    _handler = new CircuitIdentityHandler(_authentication, _circuitUser, _services.GetRequiredService<IServiceScopeFactory>());
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task OnCircuitOpened_SessionOfAUserWithTokens_ShouldSetTheUserWithItsLogin()
  {
    SignedIn(SessionPrincipal.Create(42, DateTimeOffset.UnixEpoch));
    A.CallTo(() => _users.FindAsync(42, A<CancellationToken>._)).Returns(User(withTokens: true));

    await _handler.OnCircuitOpenedAsync(null!, Ct);

    _circuitUser.UserId.ShouldBe(42);
    _circuitUser.Login.ShouldBe("octocat");
    _circuitUser.IsAdmin.ShouldBeFalse();
  }

  [Fact]
  public async Task OnCircuitOpened_UserWithoutTokens_ShouldStayAnonymous()
  {
    SignedIn(SessionPrincipal.Create(42, DateTimeOffset.UnixEpoch));
    A.CallTo(() => _users.FindAsync(42, A<CancellationToken>._)).Returns(User(withTokens: false));

    await _handler.OnCircuitOpenedAsync(null!, Ct);

    _circuitUser.IsAuthenticated.ShouldBeFalse();
  }

  [Fact]
  public async Task OnCircuitOpened_Anonymous_ShouldReadNothing()
  {
    SignedIn(new ClaimsPrincipal(new ClaimsIdentity()));

    await _handler.OnCircuitOpenedAsync(null!, Ct);

    _circuitUser.IsAuthenticated.ShouldBeFalse();
    A.CallTo(_users).MustNotHaveHappened();
  }

  public void Dispose() => _services.Dispose();

  private void SignedIn(ClaimsPrincipal principal)
  {
    A.CallTo(() => _authentication.GetAuthenticationStateAsync()).Returns(new AuthenticationState(principal));
  }

  private static AppUser User(bool withTokens)
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, 42L);

    if (withTokens)
    {
      var protector = A.Fake<ISecretProtector>();
      A.CallTo(() => protector.Protect(A<string>._)).Returns("protected");
      user.ReplaceTokens(protector, "ghu_a", null, "ghr_r", null);
    }

    return user;
  }

  #endregion
}
