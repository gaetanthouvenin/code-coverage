// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeApiTokenCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Access.Commands.RevokeApiToken;
using CodeCoverage.Application.Features.Access.Queries.ListApiTokens;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.RevokeApiToken;

public sealed class RevokeApiTokenCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IApiTokenRepository _tokens = A.Fake<IApiTokenRepository>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly RevokeApiTokenCommandHandler _handler;

  #endregion

  #region Ctors

  public RevokeApiTokenCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _currentUser.UserId).Returns(42L);

    _handler = new RevokeApiTokenCommandHandler(_currentUser, _tokens, _auditLog, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_OwnToken_ShouldRevokeItAndRecordIt()
  {
    var token = Owned(5, organizationId: 9);

    await _handler.Handle(new RevokeApiTokenCommand(5), Ct);

    token.RevokedAt.ShouldBe(Now);
    A.CallTo(() => _tokens.UpdateAsync(token, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(9, RevokeApiTokenCommandHandler.AuditAction, "5", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // An instance token is an event of the instance.
  [Fact]
  public async Task Handle_InstanceToken_ShouldRecordItForTheInstance()
  {
    Owned(5, organizationId: null);

    await _handler.Handle(new RevokeApiTokenCommand(5), Ct);

    A.CallTo(() => _auditLog.RecordAsync(RevokeApiTokenCommandHandler.AuditAction, "5", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_TokenOfAnotherUser_ShouldThrowNotFound()
  {
    A.CallTo(() => _tokens.FindOfUserAsync(42, 5, A<CancellationToken>._)).Returns((ApiToken?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new RevokeApiTokenCommand(5), Ct));

    exception.ErrorCode.ShouldBe(AccessErrors.ApiTokenNotFound);
  }

  // A request authenticated by a token of an organization reaches the tokens of that organization only.
  [Fact]
  public async Task Handle_FromATokenOfAnotherOrganization_ShouldThrowNotFound()
  {
    Owned(5, organizationId: 9);
    A.CallTo(() => _currentUser.TokenOrganizationId).Returns(10L);

    await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new RevokeApiTokenCommand(5), Ct));

    A.CallTo(() => _tokens.UpdateAsync(A<ApiToken>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_AlreadyRevoked_ShouldRecordNothing()
  {
    Owned(5, organizationId: null).Revoke(Now.AddDays(-1));

    await _handler.Handle(new RevokeApiTokenCommand(5), Ct);

    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task List_FromATokenOfTheInstance_ShouldListTheTokensOfTheInstanceOnly()
  {
    var instance = Token(1, organizationId: null);
    A.CallTo(() => _tokens.ListOfUserAsync(42, A<CancellationToken>._)).Returns([Token(2, organizationId: 9), instance]);
    A.CallTo(() => _currentUser.IsInstanceToken).Returns(true);

    var listed = await new ListApiTokensQueryHandler(_currentUser, _tokens).Handle(new ListApiTokensQuery(), Ct);

    listed.ShouldBe([instance]);
  }

  [Fact]
  public async Task List_OnTheWeb_ShouldListEveryTokenOfTheUser()
  {
    A.CallTo(() => _tokens.ListOfUserAsync(42, A<CancellationToken>._)).Returns([Token(2, organizationId: 9), Token(1, organizationId: null)]);

    var listed = await new ListApiTokensQueryHandler(_currentUser, _tokens).Handle(new ListApiTokensQuery(), Ct);

    listed.Count.ShouldBe(2);
  }

  private static ApiToken Token(long id, long? organizationId)
  {
    var token = ApiToken.Create(42, organizationId, "ci", "cvp_abcd", new byte[ApiToken.HashLength], Now.AddDays(-2), null);
    typeof(ApiToken).GetProperty(nameof(ApiToken.Id))!.SetValue(token, id);

    return token;
  }

  private ApiToken Owned(long id, long? organizationId)
  {
    var token = Token(id, organizationId);
    A.CallTo(() => _tokens.FindOfUserAsync(42, id, A<CancellationToken>._)).Returns(token);

    return token;
  }

  #endregion
}
