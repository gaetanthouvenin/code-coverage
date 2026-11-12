// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserMembershipsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access.Commands.RefreshUserMemberships;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.RefreshUserMemberships;

public sealed class RefreshUserMembershipsCommandHandlerTests
{
  #region Fields

  private const long UserId = 7;

  private const string Token = "ghu_user_token";

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly IGitHubUserTokenProvider _tokens = A.Fake<IGitHubUserTokenProvider>();

  private readonly IGitHubUserGateway _userGateway = A.Fake<IGitHubUserGateway>();

  private readonly IGitHubAppGateway _appGateway = A.Fake<IGitHubAppGateway>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IOrganizationMembershipRepository _memberships = A.Fake<IOrganizationMembershipRepository>();

  private readonly RefreshUserMembershipsCommandHandler _handler;

  private IReadOnlyDictionary<long, OrganizationRole>? _saved;

  #endregion

  #region Ctors

  public RefreshUserMembershipsCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();
    var user = AppUser.Create(Forge.GitHub, "583231", "octocat", null);

    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, UserId);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _users.FindAsync(UserId, A<CancellationToken>._)).Returns(user);
    A.CallTo(() => _tokens.GetTokenAsync(UserId, A<CancellationToken>._)).Returns(Token);
    A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(A<long>._, A<string>._, A<string>._, A<CancellationToken>._))
     .Returns((GitHubOrganizationMembership?)null);
    A.CallTo(() => _memberships.ReplaceAsync(UserId, A<IReadOnlyDictionary<long, OrganizationRole>>._, Now, A<CancellationToken>._))
     .Invokes((long _, IReadOnlyDictionary<long, OrganizationRole> roles, DateTimeOffset _, CancellationToken _) => _saved = roles);

    var updater = new MembershipUpdater(_appGateway, _directory, _memberships, clock, NullLogger<MembershipUpdater>.Instance);

    _handler = new RefreshUserMembershipsCommandHandler(_users, _tokens, _userGateway, updater);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ActiveOrganizationAdmin_ShouldBeAdmin()
  {
    var organization = _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    Membership(42, "octo-org", active: true, admin: true);

    await HandleAsync();

    _saved.ShouldNotBeNull().ShouldBe(new Dictionary<long, OrganizationRole> { [organization.Id] = OrganizationRole.Admin });
    // Asked with the token of the installation, for the login of the user.
    A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(42, "octo-org", "octocat", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userGateway.ListInstallationsAsync(Token, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // F11. GitHub describes the account that holds the login today: another one than the user, who was renamed.
  [Fact]
  public async Task Handle_MembershipOfTheAccountThatTookTheLogin_ShouldNotBeAdmin()
  {
    var organization = _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(42, "octo-org", "octocat", A<CancellationToken>._))
     .Returns(new GitHubOrganizationMembership(true, true, "999"));

    await HandleAsync();

    _saved.ShouldNotBeNull()[organization.Id].ShouldBe(OrganizationRole.Participant);
  }

  // S8. One organization refuses to tell the membership: it gets no row, and the others are refreshed all the same.
  [Fact]
  public async Task Handle_OneOrganizationRefusesToTell_ShouldFailClosedForItAlone()
  {
    var refusing = _directory.Add("1001", "refusing-org", admitted: true);
    var other = _directory.Add("2002", "other-org", admitted: true);
    Reaches(Installed(42, "1001", "refusing-org"), Installed(43, "2002", "other-org"));
    A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(42, "refusing-org", "octocat", A<CancellationToken>._))
     .Throws(new ForbiddenException("Security.Forbidden", "SAML enforcement"));
    Membership(43, "other-org", active: true, admin: true);

    await HandleAsync();

    _saved.ShouldNotBeNull().ShouldBe(new Dictionary<long, OrganizationRole> { [other.Id] = OrganizationRole.Admin });
    _saved.ShouldNotContainKey(refusing.Id);
  }

  [Fact]
  public async Task Handle_ActiveOrganizationMember_ShouldBeParticipant()
  {
    var organization = _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    Membership(42, "octo-org", active: true, admin: false);

    await HandleAsync();

    _saved.ShouldNotBeNull()[organization.Id].ShouldBe(OrganizationRole.Participant);
  }

  // Not a member of the organization, yet reaches its installation through a repository: takes part, never administers.
  [Fact]
  public async Task Handle_OutsideCollaborator_ShouldBeParticipant()
  {
    var organization = _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));

    await HandleAsync();

    _saved.ShouldNotBeNull()[organization.Id].ShouldBe(OrganizationRole.Participant);
  }

  // Invited to own the organization, not yet accepted: GitHub says admin and pending.
  [Fact]
  public async Task Handle_PendingInvitation_ShouldNotBeAdmin()
  {
    var organization = _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    Membership(42, "octo-org", active: false, admin: true);

    await HandleAsync();

    _saved.ShouldNotBeNull()[organization.Id].ShouldBe(OrganizationRole.Participant);
  }

  // The installation of the organization is no longer among those the user reaches: its row is not in what is saved,
  // and the repository removes what is not listed.
  [Fact]
  public async Task Handle_UserLeftOrganization_ShouldRemoveRow()
  {
    var left = _directory.Add("1001", "left-org", admitted: true);
    var stays = _directory.Add("2002", "stays-org", admitted: true);
    Reaches(Installed(43, "2002", "stays-org"));

    await HandleAsync();

    _saved.ShouldNotBeNull().Keys.ShouldBe([stays.Id]);
    _saved.ShouldNotContainKey(left.Id);
    A.CallTo(() => _memberships.ReplaceAsync(UserId, A<IReadOnlyDictionary<long, OrganizationRole>>._, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NonActiveOrganization_ShouldNotCreateRow()
  {
    _directory.Add("1001", "pending-org");
    _directory.Add("2002", "suspended-org", admitted: true).Suspend(SuspensionReason.Administrator, Now);
    var active = _directory.Add("3003", "active-org", admitted: true);
    Reaches(Installed(42, "1001", "pending-org"), Installed(43, "2002", "suspended-org"), Installed(44, "3003", "active-org"), Installed(45, "4004", "unknown-org"));

    await HandleAsync();

    _saved.ShouldNotBeNull().Keys.ShouldBe([active.Id]);
    // Nothing is asked about an organization that gets no row, and nothing here creates an organization.
    A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(A<long>._, A<string>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    _directory.Upserts.ShouldBe(0);
    _directory.All.Count.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_SuspendedInstallation_ShouldLinkNobody()
  {
    _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org", suspended: true));

    await HandleAsync();

    _saved.ShouldNotBeNull().ShouldBeEmpty();
    A.CallTo(_appGateway).MustNotHaveHappened();
  }

  // GitHub has no membership for a personal account: its owner administers it, a collaborator takes part.
  [Fact]
  public async Task Handle_PersonalAccounts_ShouldMakeTheOwnerAdminWithoutAskingForAMembership()
  {
    var own = _directory.Add("583231", "octocat", admitted: true, accountType: OrganizationAccountType.User);
    var other = _directory.Add("999", "someone-else", admitted: true, accountType: OrganizationAccountType.User);
    Reaches(
      Installed(42, "583231", "octocat", accountType: OrganizationAccountType.User),
      Installed(43, "999", "someone-else", accountType: OrganizationAccountType.User)
    );

    await HandleAsync();

    _saved.ShouldNotBeNull().ShouldBe(new Dictionary<long, OrganizationRole> { [own.Id] = OrganizationRole.Admin, [other.Id] = OrganizationRole.Participant }, ignoreOrder: true);
    A.CallTo(_appGateway).MustNotHaveHappened();
  }

  // Fail closed: the session with GitHub is over, so nothing proves a link any more.
  [Fact]
  public async Task Handle_UserWithoutATokenGitHubAccepts_ShouldRemoveEveryMembership()
  {
    _directory.Add("1001", "octo-org", admitted: true);
    A.CallTo(() => _tokens.GetTokenAsync(UserId, A<CancellationToken>._)).Throws(new UnauthorizedException("Access.Unauthenticated", "expired"));

    await HandleAsync();

    _saved.ShouldNotBeNull().ShouldBeEmpty();
    A.CallTo(_userGateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_TokenRefusedByGitHubWhileListing_ShouldRemoveEveryMembership()
  {
    A.CallTo(() => _userGateway.ListInstallationsAsync(Token, A<CancellationToken>._)).Throws(new UnauthorizedException("Access.Unauthenticated", "revoked"));

    await HandleAsync();

    _saved.ShouldNotBeNull().ShouldBeEmpty();
  }

  // Fail closed the other way: when GitHub cannot be asked, nothing is written. The rows keep their date, and stop
  // granting anything once the hard expiry has passed.
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Handle_GitHubUnavailable_ShouldWriteNothingAndFail(bool whileListing)
  {
    _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    var failure = new BadGatewayException("GitHub.Unavailable", "GitHub does not answer.");

    if (whileListing)
    {
      A.CallTo(() => _userGateway.ListInstallationsAsync(Token, A<CancellationToken>._)).Throws(failure);
    }
    else
    {
      A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(A<long>._, A<string>._, A<string>._, A<CancellationToken>._)).Throws(failure);
    }

    await Should.ThrowAsync<BadGatewayException>(async () => await HandleAsync());

    A.CallTo(_memberships).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownUser_ShouldDoNothing()
  {
    A.CallTo(() => _users.FindAsync(UserId, A<CancellationToken>._)).Returns((AppUser?)null);

    await HandleAsync();

    A.CallTo(_tokens).MustNotHaveHappened();
    A.CallTo(_memberships).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static GitHubInstallationInfo Installed(
    long id,
    string accountId,
    string login,
    OrganizationAccountType accountType = OrganizationAccountType.Organization,
    bool suspended = false) =>
    new(id, accountId, login, accountType, suspended);

  private async Task HandleAsync() => await _handler.Handle(new RefreshUserMembershipsCommand(UserId), Ct);

  private void Reaches(params GitHubInstallationInfo[] installations)
  {
    A.CallTo(() => _userGateway.ListInstallationsAsync(Token, A<CancellationToken>._)).Returns(installations);
  }

  private void Membership(long installationId, string organization, bool active, bool admin)
  {
    A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(installationId, organization, "octocat", A<CancellationToken>._))
     .Returns(new GitHubOrganizationMembership(active, admin, "583231"));
  }

  #endregion
}
