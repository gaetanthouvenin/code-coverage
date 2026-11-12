// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Domain.Tests.Tokens;

public sealed class ApiTokenTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_OfTheInstance_ShouldHaveNoOrganization()
  {
    var token = ApiToken.Create(42, null, "ops", "cvp_abcd", new byte[ApiToken.HashLength], Now, null);

    token.IsInstanceToken.ShouldBeTrue();
    token.OrganizationId.ShouldBeNull();
    token.IsUsableAt(Now.AddYears(5)).ShouldBeTrue();
  }

  [Fact]
  public void IsUsableAt_ShouldStopAtTheExpiryAndAfterARevocation()
  {
    var token = ApiToken.Create(42, 9, "ci", "cvp_abcd", new byte[ApiToken.HashLength], Now, Now.AddDays(1));

    token.IsUsableAt(Now.AddHours(23)).ShouldBeTrue();
    token.IsUsableAt(Now.AddDays(1)).ShouldBeFalse();

    token.Revoke(Now.AddHours(1));
    token.Revoke(Now.AddHours(2));

    token.RevokedAt.ShouldBe(Now.AddHours(1));
    token.IsUsableAt(Now.AddHours(1)).ShouldBeFalse();
  }

  [Fact]
  public void Create_InvalidValues_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => ApiToken.Create(0, null, "ci", "cvp_abcd", new byte[32], Now, null));
    Should.Throw<ArgumentOutOfRangeException>(() => ApiToken.Create(42, 0, "ci", "cvp_abcd", new byte[32], Now, null));
    Should.Throw<ArgumentException>(() => ApiToken.Create(42, null, " ", "cvp_abcd", new byte[32], Now, null));
    Should.Throw<ArgumentException>(() => ApiToken.Create(42, null, new string('n', ApiToken.NameMaxLength + 1), "cvp_abcd", new byte[32], Now, null));
    Should.Throw<ArgumentException>(() => ApiToken.Create(42, null, "ci", "cvp_abcd", new byte[31], Now, null));
    Should.Throw<ArgumentException>(() => ApiToken.Create(42, null, "ci", "cvp_abcd", new byte[32], Now, Now));
  }

  [Fact]
  public void IssuedApiToken_ToString_ShouldNeverShowTheValue()
  {
    var token = ApiToken.Create(42, null, "ops", "cvp_abcd", new byte[ApiToken.HashLength], Now, null);

    new IssuedApiToken(token, "cvp_abcd_the_secret_rest").ToString().ShouldNotContain("the_secret_rest");
  }

  #endregion
}
