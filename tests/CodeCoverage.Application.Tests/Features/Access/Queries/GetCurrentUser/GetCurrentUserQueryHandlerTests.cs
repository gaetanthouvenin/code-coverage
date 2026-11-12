// ------------------------------------------------------------------------------------------------
// <copyright file="GetCurrentUserQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access.Queries.GetCurrentUser;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandlerTests
{
  #region Fields

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly GetCurrentUserQueryHandler _handler;

  #endregion

  #region Ctors

  public GetCurrentUserQueryHandlerTests()
  {
    _handler = new GetCurrentUserQueryHandler(_currentUser, _users);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_SignedInUser_ShouldReturnItsRow()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    A.CallTo(() => _currentUser.UserId).Returns(7);
    A.CallTo(() => _users.FindAsync(7, A<CancellationToken>._)).Returns(user);

    (await _handler.Handle(new GetCurrentUserQuery(), Ct)).ShouldBeSameAs(user);
  }

  [Fact]
  public async Task Handle_DeletedUser_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _currentUser.UserId).Returns(7);
    A.CallTo(() => _users.FindAsync(7, A<CancellationToken>._)).Returns((AppUser?)null);

    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await _handler.Handle(new GetCurrentUserQuery(), Ct));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
  }

  [Fact]
  public async Task Handle_PrincipalWithoutUser_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _currentUser.UserId).Returns(null);

    await Should.ThrowAsync<UnauthorizedException>(async () => await _handler.Handle(new GetCurrentUserQuery(), Ct));

    A.CallTo(_users).MustNotHaveHappened();
  }

  #endregion
}
