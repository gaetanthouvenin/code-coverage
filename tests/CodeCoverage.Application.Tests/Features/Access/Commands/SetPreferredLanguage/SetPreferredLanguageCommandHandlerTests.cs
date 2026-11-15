// ------------------------------------------------------------------------------------------------
// <copyright file="SetPreferredLanguageCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access.Commands.SetPreferredLanguage;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.SetPreferredLanguage;

public sealed class SetPreferredLanguageCommandHandlerTests
{
  #region Fields

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly SetPreferredLanguageCommandHandler _handler;

  #endregion

  #region Ctors

  public SetPreferredLanguageCommandHandlerTests()
  {
    A.CallTo(() => _currentUser.UserId).Returns(42L);

    _handler = new SetPreferredLanguageCommandHandler(_currentUser, _users);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_NewLanguage_ShouldSaveItOnTheUser()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    A.CallTo(() => _users.FindAsync(42L, A<CancellationToken>._)).Returns(user);

    await _handler.Handle(new SetPreferredLanguageCommand("it"), Ct);

    user.PreferredLanguage.ShouldBe("it");
    A.CallTo(() => _users.UpdateAsync(user, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_SameLanguage_ShouldSaveNothing()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);
    user.SetPreferredLanguage("es");
    A.CallTo(() => _users.FindAsync(42L, A<CancellationToken>._)).Returns(user);

    await _handler.Handle(new SetPreferredLanguageCommand("es"), Ct);

    A.CallTo(() => _users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_WithoutUser_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _currentUser.UserId).Returns(null);

    await Should.ThrowAsync<UnauthorizedException>(async () => await _handler.Handle(new SetPreferredLanguageCommand("fr"), Ct));
  }

  #endregion
}
