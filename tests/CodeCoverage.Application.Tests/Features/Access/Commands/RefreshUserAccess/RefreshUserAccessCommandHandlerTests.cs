// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserAccessCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access.Commands.RefreshUserAccess;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.RefreshUserAccess;

public sealed class RefreshUserAccessCommandHandlerTests
{
  #region Fields

  private const long UserId = 7;

  private const string Token = "ghu_user_token";

  private const string RenewedToken = "ghu_renewed_token";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly ForgeUserAccount OtherOrg = new("41", "999", "other-org", OrganizationAccountType.Organization, false);

  private static readonly ForgeUserAccount OctoOrg = new("42", "1001", "octo-org", OrganizationAccountType.Organization, false);

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly IForgeUserTokenProvider _tokens = A.Fake<IForgeUserTokenProvider>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeUserGateway _userGateway = A.Fake<IForgeUserGateway>();

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
    A.CallTo(() => _gateways.GetUserTokenProvider(Forge.GitHub)).Returns(_tokens);
    A.CallTo(() => _gateways.GetUserGateway(Forge.GitHub)).Returns(_userGateway);
    A.CallTo(() => _tokens.GetTokenAsync(UserId, A<CancellationToken>._)).Returns(Token);
    A.CallTo(() => _userGateway.ListAccountsAsync(Token, A<CancellationToken>._)).Returns([OtherOrg, OctoOrg]);
    _directory.Current = _directory.Add("1001", "octo-org", admitted: true);

    _handler = new RefreshUserAccessCommandHandler(_users, _gateways, _directory, _accesses, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ForgePermissions_ShouldReplaceTheAccess()
  {
    A.CallTo(() => _userGateway.ListRepositoryPermissionsAsync(Token, OctoOrg, A<CancellationToken>._))
     .Returns([new ForgeRepositoryPermission("100", RepositoryPermission.Admin), new ForgeRepositoryPermission("200", RepositoryPermission.Write)]);
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._))
     .Returns([RepositoryAccess.Create(UserId, "100", RepositoryPermission.Read, Now), RepositoryAccess.Create(UserId, "300", RepositoryPermission.Admin, Now.AddHours(-1))]);

    await HandleAsync();

    A.CallTo(() => _accesses.UpsertAsync(UserId, "100", RepositoryPermission.Admin, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _accesses.UpsertAsync(UserId, "200", RepositoryPermission.Write, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _accesses.RemoveAsync(UserId, "300", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _accesses.RemoveAsync(UserId, "100", A<CancellationToken>._)).MustNotHaveHappened();
    // Only the account of the organization of the scope is read.
    A.CallTo(() => _userGateway.ListRepositoryPermissionsAsync(Token, OtherOrg, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The renewal of an expired token belongs to the provider of the forge of the user: the handler only uses what it gives.
  [Fact]
  public async Task Handle_ExpiredToken_ShouldRenewThroughTheForgeProvider()
  {
    A.CallTo(() => _tokens.GetTokenAsync(UserId, A<CancellationToken>._)).Returns(RenewedToken);
    A.CallTo(() => _userGateway.ListAccountsAsync(RenewedToken, A<CancellationToken>._)).Returns([OctoOrg]);
    A.CallTo(() => _userGateway.ListRepositoryPermissionsAsync(RenewedToken, OctoOrg, A<CancellationToken>._))
     .Returns([new ForgeRepositoryPermission("100", RepositoryPermission.Read)]);

    await HandleAsync();

    A.CallTo(() => _gateways.GetUserTokenProvider(Forge.GitHub)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _gateways.GetUserTokenProvider(A<Forge>.That.Not.IsEqualTo(Forge.GitHub))).MustNotHaveHappened();
    A.CallTo(() => _accesses.UpsertAsync(UserId, "100", RepositoryPermission.Read, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userGateway.ListAccountsAsync(Token, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Fail closed: without a token its forge accepts, nothing proves an access any more.
  [Fact]
  public async Task Handle_UserWithoutATokenItsForgeAccepts_ShouldRemoveEveryRow()
  {
    A.CallTo(() => _tokens.GetTokenAsync(UserId, A<CancellationToken>._)).Throws(new UnauthorizedException("Access.Unauthenticated", "expired"));
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([RepositoryAccess.Create(UserId, "100", RepositoryPermission.Read, Now)]);

    await HandleAsync();

    A.CallTo(() => _accesses.RemoveAsync(UserId, "100", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _accesses.UpsertAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_AccountNotReachedOrSuspended_ShouldRemoveEveryRow()
  {
    A.CallTo(() => _userGateway.ListAccountsAsync(Token, A<CancellationToken>._)).Returns([OctoOrg with { Suspended = true }]);
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([RepositoryAccess.Create(UserId, "100", RepositoryPermission.Read, Now)]);

    await HandleAsync();

    A.CallTo(() => _accesses.RemoveAsync(UserId, "100", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userGateway.ListRepositoryPermissionsAsync(A<string>._, A<ForgeUserAccount>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // An account of the forge of the user never grants anything in an organization of another forge, even with its identifier.
  [Fact]
  public async Task Handle_OrganizationOfAnotherForge_ShouldRemoveEveryRowWithoutAskingAnyForge()
  {
    var organization = Organization.CreatePending(Forge.GitLab, "1001", "octo-org", OrganizationAccountType.Organization, Now);

    organization.Admit("tests", Now);
    _directory.Current = _directory.Add(organization);
    A.CallTo(() => _accesses.ListOfUserAsync(UserId, A<CancellationToken>._)).Returns([RepositoryAccess.Create(UserId, "100", RepositoryPermission.Read, Now)]);

    await HandleAsync();

    A.CallTo(() => _accesses.RemoveAsync(UserId, "100", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(_tokens).MustNotHaveHappened();
    A.CallTo(_userGateway).MustNotHaveHappened();
  }

  // Fail closed the other way: when the forge cannot be asked, nothing is written and the rows age out.
  [Fact]
  public async Task Handle_ForgeUnavailable_ShouldWriteNothingAndFail()
  {
    A.CallTo(() => _userGateway.ListRepositoryPermissionsAsync(Token, OctoOrg, A<CancellationToken>._)).Throws(new BadGatewayException("Forge.Unavailable", "down"));

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

  [Fact]
  public async Task Handle_NoOrganizationInTheScope_ShouldDoNothing()
  {
    _directory.Current = null;

    await HandleAsync();

    A.CallTo(_tokens).MustNotHaveHappened();
    A.CallTo(_accesses).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private async Task HandleAsync() => await _handler.Handle(new RefreshUserAccessCommand(UserId), Ct);

  #endregion
}
