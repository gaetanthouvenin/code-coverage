// ------------------------------------------------------------------------------------------------
// <copyright file="AppUserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Tests.Users;

public sealed class AppUserTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  #endregion

  #region Ctors

  public AppUserTests()
  {
    A.CallTo(() => _protector.Protect(A<string>._)).ReturnsLazily((string value) => $"protected({value.Length})");
  }

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldHoldTheAccountAndNoToken()
  {
    var user = New();

    user.Forge.ShouldBe(Forge.GitHub);
    user.ExternalId.ShouldBe("583231");
    user.Login.ShouldBe("octocat");
    user.AvatarUrl.ShouldBe("https://avatars.example/u/1");
    user.AccessTokenProtected.ShouldBeNull();
    user.RefreshTokenProtected.ShouldBeNull();
    user.IsAdmin.ShouldBeFalse();
    user.LastLoginAt.ShouldBeNull();
    user.Version.ShouldBe(0);
  }

  [Fact]
  public void Create_ExternalIdInUpperCase_ShouldBeLowered()
  {
    AppUser.Create(Forge.AzureDevOps, "ABC-DEF", "octocat", null).ExternalId.ShouldBe("abc-def");
  }

  [Theory]
  [InlineData("", "octocat")]
  [InlineData(" ", "octocat")]
  [InlineData("1", "")]
  public void Create_Blank_ShouldThrow(string externalId, string login)
  {
    Should.Throw<ArgumentException>(() => AppUser.Create(Forge.GitHub, externalId, login, null));
  }

  [Fact]
  public void Create_TooLong_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => AppUser.Create(Forge.GitHub, new string('1', AppUser.ExternalIdMaxLength + 1), "octocat", null));
    Should.Throw<ArgumentException>(() => AppUser.Create(Forge.GitHub, "1", new string('l', AppUser.LoginMaxLength + 1), null));
  }

  [Fact]
  public void Create_AvatarTooLong_ShouldKeepNone()
  {
    AppUser.Create(Forge.GitHub, "1", "octocat", new string('a', AppUser.AvatarUrlMaxLength + 1)).AvatarUrl.ShouldBeNull();
  }

  [Fact]
  public void ReplaceTokens_ShouldStoreProtectedValuesOnly()
  {
    var user = New();
    var access = "ghu_access_in_clear";
    var refresh = "ghr_refresh_in_clear";

    user.ReplaceTokens(_protector, access, Now.AddHours(8), refresh, Now.AddMonths(6));

    user.AccessTokenProtected.ShouldBe($"protected({access.Length})");
    user.RefreshTokenProtected.ShouldBe($"protected({refresh.Length})");
    user.AccessTokenProtected.ShouldNotBeNull().ShouldNotContain(access);
    user.RefreshTokenProtected.ShouldNotBeNull().ShouldNotContain(refresh);
    user.AccessTokenExpiresAt.ShouldBe(Now.AddHours(8));
    user.RefreshTokenExpiresAt.ShouldBe(Now.AddMonths(6));
    user.Version.ShouldBe(1);
  }

  [Fact]
  public void ReplaceTokens_WithoutRefreshToken_ShouldClearTheOldOne()
  {
    var user = New();
    user.ReplaceTokens(_protector, "access", Now, "refresh", Now);

    user.ReplaceTokens(_protector, "access-2", null, null, Now.AddDays(1));

    user.RefreshTokenProtected.ShouldBeNull();
    user.RefreshTokenExpiresAt.ShouldBeNull();
    user.AccessTokenExpiresAt.ShouldBeNull();
  }

  [Fact]
  public void ClearTokens_ShouldForgetBothTokensAndTheirExpiries()
  {
    var user = New();
    user.ReplaceTokens(_protector, "access", Now.AddHours(8), "refresh", Now.AddMonths(6));
    var version = user.Version;

    user.ClearTokens();

    user.AccessTokenProtected.ShouldBeNull();
    user.RefreshTokenProtected.ShouldBeNull();
    user.AccessTokenExpiresAt.ShouldBeNull();
    user.RefreshTokenExpiresAt.ShouldBeNull();
    user.Version.ShouldBe(version + 1);
  }

  [Fact]
  public void ClearTokens_WithoutAnyToken_ShouldChangeNothing()
  {
    var user = New();

    user.ClearTokens();

    user.Version.ShouldBe(0);
  }

  [Fact]
  public void ReplaceTokens_BlankAccessToken_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => New().ReplaceTokens(_protector, " ", Now, null, null));
  }

  [Fact]
  public void RecordSignIn_ShouldApplyTheAccountAndIncrementTheVersion()
  {
    var user = New();

    user.RecordSignIn("renamed", null, Now);

    user.Login.ShouldBe("renamed");
    user.AvatarUrl.ShouldBeNull();
    user.LastLoginAt.ShouldBe(Now);
    user.Version.ShouldBe(1);
  }

  [Fact]
  public void SetInstanceAdmin_ShouldOnlyCountAChange()
  {
    var user = New();

    user.SetInstanceAdmin(false);
    user.Version.ShouldBe(0);

    user.SetInstanceAdmin(true);
    user.IsAdmin.ShouldBeTrue();
    user.Version.ShouldBe(1);
  }

  private static AppUser New() => AppUser.Create(Forge.GitHub, "583231", "octocat", "https://avatars.example/u/1");

  #endregion
}
