// ------------------------------------------------------------------------------------------------
// <copyright file="SessionPrincipalTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Claims;

using CodeCoverage.Web.Configuration.Authentication;

namespace CodeCoverage.Web.Tests.Authentication;

public sealed class SessionPrincipalTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  // The acceptance of M3.03: the cookie holds the identifier of the user and a timestamp, no login, no organization.
  [Fact]
  public void Create_ShouldHoldOnlyTheUserIdAndTheTimeOfTheSignIn()
  {
    var principal = SessionPrincipal.Create(42, Now);

    principal.Claims.Select(static c => (c.Type, c.Value))
             .ShouldBe([(SessionPrincipal.UserIdClaim, "42"), (SessionPrincipal.SessionClaim, Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))]);
    principal.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeTrue();
  }

  [Fact]
  public void TryReadUserId_SessionPrincipal_ShouldReadTheIdentifier()
  {
    SessionPrincipal.TryReadUserId(SessionPrincipal.Create(42, Now), out var userId).ShouldBeTrue();

    userId.ShouldBe(42);
  }

  [Theory]
  [InlineData("0")]
  [InlineData("-3")]
  [InlineData("forty-two")]
  public void TryReadUserId_InvalidIdentifier_ShouldBeFalse(string value)
  {
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SessionPrincipal.UserIdClaim, value)], "Cookies"));

    SessionPrincipal.TryReadUserId(principal, out _).ShouldBeFalse();
  }

  [Fact]
  public void TryReadUserId_Anonymous_ShouldBeFalse()
  {
    var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SessionPrincipal.UserIdClaim, "42")]));

    SessionPrincipal.TryReadUserId(anonymous, out _).ShouldBeFalse();
    SessionPrincipal.TryReadUserId(null, out _).ShouldBeFalse();
  }

  #endregion
}
