// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserAccessCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access.Commands.RefreshUserAccess;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.RefreshUserAccess;

public sealed class RefreshUserAccessCommandHandlerTests
{
  #region Fields

  private const long UserId = 7;

  private const string Token = "ghu_user_token";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly IGitHubUserTokenProvider _tokens = A.Fake<IGitHubUserTokenProvider>();

  private readonly IGitHubUserGateway _userGateway = A.Fake<IGitHubUserGateway>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IUserRepositoryAccessRepository _accesses = A.Fake<IUserRepositoryAccessRepository>();

  private readonly RefreshUserAccessCommandHandler _handler;

  #endregion

  #region Ctors

  public RefreshUserAccessCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);

    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, UserId);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _users.FindAsync(UserId, A<CancellationToken>._)).Returns(user);
    A.CallTo(() => _tokens.GetTokenAsync(UserId, A<CancellationToken>._)).Returns(Token);
    A.CallTo(() => _userGateway.ListInstallationsAsync(Token, A<CancellationToken>._))
     .Returns([new GitHubInstallationInfo(41, "999", "other-org", OrganizationAccountType.Organization, false), new GitHubInstallationInfo(42, "1001", "octo-org", OrganizationAccountType.Organization, false)]);
    _directory.Current = _directory.Add("1001", "octo-org", admitted: true);

    _handler = new RefreshUserAccessCommandHandler(_users, _tokens, _userGateway, _directory, _accesses, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldWriteTheRepositoriesOfTheInstallationOfTheOrganization()
  {
    A.CallTo(() => _userGateway.ListInstallationRepositoriesAsync(Token, 42, A<CancellationToken>._))
     .Returns([new GitHubUserRepository("100", RepositoryPermission.Admin), new GitHubUserRepository("200", RepositoryPermission.Write)]);

    await HandleAsync();

    A.CallTo(() => _accesses.UpsertAsync(UserId, "100", RepositoryPermission.Admin, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _accesses.UpsertAsync(UserId, "200", RepositoryPermission.Write, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userGateway.ListInstallationRepositoriesAsync(Token, 41, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RepositoryNoLongerListed_ShouldRemoveItsRow()
  {
    A.CallTo(() => _userGateway.ListInstallationRepositoriesAsync(Token, 42, A<CancellationToken>._)).Returns([new GitHubUserRepository("100", RepositoryPermission.Read)]);
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._))
     .Returns([RepositoryAccess.Create(UserId, "100", RepositoryPermission.Read, Now), RepositoryAccess.Create(UserId, "300", RepositoryPermission.Admin, Now.AddHours(-1))]);

    await HandleAsync();

    A.CallTo(() => _accesses.RemoveAsync(UserId, "300", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _accesses.RemoveAsync(UserId, "100", A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Fail closed: without a token GitHub accepts, nothing proves an access any more.
  [Fact]
  public async Task Handle_UserWithoutATokenGitHubAccepts_ShouldRemoveEveryRow()
  {
    A.CallTo(() => _tokens.GetTokenAsync(UserId, A<CancellationToken>._)).Throws(new UnauthorizedException("Access.Unauthenticated", "expired"));
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([RepositoryAccess.Create(UserId, "100", RepositoryPermission.Read, Now)]);

    await HandleAsync();

    A.CallTo(() => _accesses.RemoveAsync(UserId, "100", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _accesses.UpsertAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_InstallationNotReachedOrSuspended_ShouldRemoveEveryRow()
  {
    A.CallTo(() => _userGateway.ListInstallationsAsync(Token, A<CancellationToken>._))
     .Returns([new GitHubInstallationInfo(42, "1001", "octo-org", OrganizationAccountType.Organization, true)]);
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([RepositoryAccess.Create(UserId, "100", RepositoryPermission.Read, Now)]);

    await HandleAsync();

    A.CallTo(() => _accesses.RemoveAsync(UserId, "100", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userGateway.ListInstallationRepositoriesAsync(A<string>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Fail closed the other way: when GitHub cannot be asked, nothing is written and the rows age out.
  [Fact]
  public async Task Handle_GitHubUnavailable_ShouldWriteNothingAndFail()
  {
    A.CallTo(() => _userGateway.ListInstallationRepositoriesAsync(Token, 42, A<CancellationToken>._)).Throws(new BadGatewayException("GitHub.Unavailable", "down"));

    await Should.ThrowAsync<BadGatewayException>(async () => await HandleAsync());

    A.CallTo(_accesses).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownUser_ShouldDoNothing()
  {
    A.CallTo(() => _users.FindAsync(UserId, A<CancellationToken>._)).Returns((AppUser?)null);

    await HandleAsync();

    A.CallTo(_tokens).MustNotHaveHappened();
    A.CallTo(_accesses).MustNotHaveHappened();
  }

  private async Task HandleAsync() => await _handler.Handle(new RefreshUserAccessCommand(UserId), Ct);

  #endregion
}
