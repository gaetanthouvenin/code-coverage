// ------------------------------------------------------------------------------------------------
// <copyright file="GetCurrentUserAccessQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access.Queries.GetCurrentUserAccess;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Queries.GetCurrentUserAccess;

public sealed class GetCurrentUserAccessQueryHandlerTests
{
  #region Fields

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  #endregion

  #region Ctors

  public GetCurrentUserAccessQueryHandlerTests()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    A.CallTo(() => _currentUser.UserId).Returns(42L);
    A.CallTo(() => _users.FindAsync(42, A<CancellationToken>._)).Returns(user);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [InlineData(true, true)]
  [InlineData(false, false)]
  public async Task Handle_ShouldTellWhetherTheUserAdministersTheInstance(bool admin, bool expected)
  {
    A.CallTo(() => _currentUser.IsAdmin).Returns(admin);

    var access = await new GetCurrentUserAccessQueryHandler(_currentUser, _users).Handle(new GetCurrentUserAccessQuery(), Ct);

    access.User.Login.ShouldBe("octocat");
    access.IsInstanceAdministrator.ShouldBe(expected);
  }

  // A token of an organization never shows the instance.
  [Fact]
  public async Task Handle_TokenOfAnOrganization_ShouldNotAdministerTheInstance()
  {
    A.CallTo(() => _currentUser.IsAdmin).Returns(true);
    A.CallTo(() => _currentUser.TokenOrganizationId).Returns(3L);

    (await new GetCurrentUserAccessQueryHandler(_currentUser, _users).Handle(new GetCurrentUserAccessQuery(), Ct)).IsInstanceAdministrator.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_NoUser_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _currentUser.UserId).Returns(null);

    await Should.ThrowAsync<UnauthorizedException>(() => new GetCurrentUserAccessQueryHandler(_currentUser, _users).Handle(new GetCurrentUserAccessQuery(), Ct).AsTask());
  }

  #endregion
}
