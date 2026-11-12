// ------------------------------------------------------------------------------------------------
// <copyright file="AuthenticateApiTokenQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Access.Queries.AuthenticateApiToken;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Queries.AuthenticateApiToken;

public sealed class AuthenticateApiTokenQueryHandlerTests
{
  #region Fields

  private const string Value = "cvp_secret_value";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Hash = new byte[ApiToken.HashLength];

  private readonly IApiTokenRepository _tokens = A.Fake<IApiTokenRepository>();

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly AuthenticateApiTokenQueryHandler _handler;

  #endregion

  #region Ctors

  public AuthenticateApiTokenQueryHandlerTests()
  {
    var hasher = A.Fake<ITokenHasher>();
    var clock = A.Fake<IClock>();
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);

    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, 42L);
    A.CallTo(() => hasher.Hash(Value)).Returns(Hash);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _users.FindAsync(42, A<CancellationToken>._)).Returns(user);

    _handler = new AuthenticateApiTokenQueryHandler(_tokens, _users, hasher, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ValidToken_ShouldNameItsUserAndOrganizationAndRecordTheUse()
  {
    var token = Token(organizationId: 9);

    var identity = (await HandleAsync()).ShouldNotBeNull();

    identity.ShouldBe(new ApiTokenIdentity(token.Id, 42, "octocat", Forge.GitHub, "583231", 9));
    A.CallTo(() => _tokens.RecordUseAsync(token.Id, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RevokedToken_ShouldAuthenticateNobody()
  {
    Token(organizationId: null).Revoke(Now.AddMinutes(-1));

    (await HandleAsync()).ShouldBeNull();
    A.CallTo(() => _tokens.RecordUseAsync(A<long>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ExpiredToken_ShouldAuthenticateNobody()
  {
    Token(organizationId: null, expiresAt: Now);

    (await HandleAsync()).ShouldBeNull();
  }

  [Fact]
  public async Task Handle_UnknownToken_ShouldAuthenticateNobody()
  {
    A.CallTo(() => _tokens.FindByHashAsync(Hash, A<CancellationToken>._)).Returns((ApiToken?)null);

    (await HandleAsync()).ShouldBeNull();
  }

  [Fact]
  public void ToString_ShouldNotContainTheValue()
  {
    new AuthenticateApiTokenQuery(Value).ToString().ShouldNotContain(Value);
  }

  private ApiToken Token(long? organizationId, DateTimeOffset? expiresAt = null)
  {
    var token = ApiToken.Create(42, organizationId, "ci", "cvp_secr", Hash, Now.AddDays(-1), expiresAt);
    typeof(ApiToken).GetProperty(nameof(ApiToken.Id))!.SetValue(token, 5L);
    A.CallTo(() => _tokens.FindByHashAsync(Hash, A<CancellationToken>._)).Returns(token);

    return token;
  }

  private async Task<ApiTokenIdentity?> HandleAsync() => await _handler.Handle(new AuthenticateApiTokenQuery(Value), Ct);

  #endregion
}
