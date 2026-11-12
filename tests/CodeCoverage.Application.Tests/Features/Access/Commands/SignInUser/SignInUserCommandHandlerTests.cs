// ------------------------------------------------------------------------------------------------
// <copyright file="SignInUserCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Access.Commands.SignInUser;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.SignInUser;

public sealed class SignInUserCommandHandlerTests
{
  #region Fields

  private const long UserId = 7;

  private const string AccessToken = "ghu_access_token";

  private const string RefreshToken = "ghr_refresh_token";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly SignInUserCommand Command = new(AccessToken, TimeSpan.FromHours(8), RefreshToken, TimeSpan.FromDays(184));

  private readonly IGitHubUserGateway _userGateway = A.Fake<IGitHubUserGateway>();

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly IGitHubAppGateway _appGateway = A.Fake<IGitHubAppGateway>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IOrganizationMembershipRepository _memberships = A.Fake<IOrganizationMembershipRepository>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly AccessOptions _access = new();

  private readonly BootstrapOptions _bootstrap = new();

  private readonly SignInUserCommandHandler _handler;

  private readonly List<JobRequest> _queued = [];

  private AppUser? _added;

  private IReadOnlyDictionary<long, OrganizationRole>? _saved;

  #endregion

  #region Ctors

  public SignInUserCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();
    var protector = A.Fake<ISecretProtector>();
    var access = A.Fake<IOptionsMonitor<AccessOptions>>();
    var bootstrap = A.Fake<IOptionsMonitor<BootstrapOptions>>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => $"protected({value})");
    A.CallTo(() => access.CurrentValue).Returns(_access);
    A.CallTo(() => bootstrap.CurrentValue).Returns(_bootstrap);
    A.CallTo(() => _userGateway.GetUserAsync(AccessToken, A<CancellationToken>._)).Returns(new GitHubUser("583231", "octocat", "https://avatars.example/u/583231"));
    A.CallTo(() => _userGateway.ListInstallationsAsync(AccessToken, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _users.FindByExternalIdAsync(Forge.GitHub, "583231", A<CancellationToken>._)).Returns((AppUser?)null);
    A.CallTo(() => _users.TryAddAsync(A<AppUser>._, A<CancellationToken>._))
     .ReturnsLazily((AppUser user, CancellationToken _) =>
     {
       WithId(user);
       _added = user;
       return true;
     });
    A.CallTo(() => _appGateway.GetOrganizationMembershipAsync(A<long>._, A<string>._, A<string>._, A<CancellationToken>._))
     .Returns((GitHubOrganizationMembership?)null);
    A.CallTo(() => _memberships.ReplaceAsync(UserId, A<IReadOnlyDictionary<long, OrganizationRole>>._, Now, A<CancellationToken>._))
     .Invokes((long _, IReadOnlyDictionary<long, OrganizationRole> roles, DateTimeOffset _, CancellationToken _) => _saved = roles);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).Invokes((JobRequest job, CancellationToken _) => _queued.Add(job)).Returns(1L);

    var updater = new MembershipUpdater(_appGateway, _directory, _memberships, clock, NullLogger<MembershipUpdater>.Instance);

    _handler = new SignInUserCommandHandler(_userGateway, _users, protector, updater, _jobQueue, access, bootstrap, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_NoAccessibleRepository_ShouldThrowForbidden()
  {
    var exception = await Should.ThrowAsync<ForbiddenException>(async () => await HandleAsync());

    exception.ErrorCode.ShouldBe(AccessErrors.NoRepository);
    // The row and its tokens are kept for a later refresh; no refresh is queued and no membership is left.
    _added.ShouldNotBeNull().AccessTokenProtected.ShouldBe($"protected({AccessToken})");
    _saved.ShouldNotBeNull().ShouldBeEmpty();
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NoActiveOrganization_ShouldThrowForbidden()
  {
    _directory.Add("1001", "pending-org");
    _directory.Add("2002", "suspended-org", admitted: true).Suspend(SuspensionReason.Administrator, Now);
    Reaches(Installed(42, "1001", "pending-org"), Installed(43, "2002", "suspended-org"), Installed(44, "3003", "unknown-org"));

    var exception = await Should.ThrowAsync<ForbiddenException>(async () => await HandleAsync());

    exception.ErrorCode.ShouldBe(AccessErrors.NoRepository);
    _saved.ShouldNotBeNull().ShouldBeEmpty();
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_InstanceAdminWithoutOrganization_ShouldSignIn()
  {
    _access.AdminUserIds = "1234, 583231";

    var userId = await HandleAsync();

    userId.ShouldBe(UserId);
    _saved.ShouldNotBeNull().ShouldBeEmpty();
    // The stored flag is not written: the lists are read again on each request.
    _added.ShouldNotBeNull().IsAdmin.ShouldBeFalse();
    _queued.ShouldHaveSingleItem().LockKey.ShouldBe(RefreshUserMembershipsJobs.Create(UserId).LockKey);
  }

  [Fact]
  public async Task Handle_BootstrapAdminWithoutOrganization_ShouldSignIn()
  {
    _bootstrap.AdminUserIds = "583231";

    (await HandleAsync()).ShouldBe(UserId);
  }

  [Theory]
  [InlineData("58323")]
  [InlineData("not-a-number")]
  [InlineData("")]
  public async Task Handle_AdminListNamingSomeoneElse_ShouldThrowForbidden(string list)
  {
    _access.AdminUserIds = list;

    await Should.ThrowAsync<ForbiddenException>(async () => await HandleAsync());
  }

  [Fact]
  public async Task Handle_ActiveOrganization_ShouldSaveTheUserItsTokensAndItsMembership()
  {
    var organization = _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));

    var userId = await HandleAsync();

    userId.ShouldBe(UserId);
    var user = _added.ShouldNotBeNull();
    user.Login.ShouldBe("octocat");
    user.AvatarUrl.ShouldBe("https://avatars.example/u/583231");
    user.LastLoginAt.ShouldBe(Now);
    user.AccessTokenProtected.ShouldBe($"protected({AccessToken})");
    user.AccessTokenExpiresAt.ShouldBe(Now.AddHours(8));
    user.RefreshTokenProtected.ShouldBe($"protected({RefreshToken})");
    user.RefreshTokenExpiresAt.ShouldBe(Now.AddDays(184));
    _saved.ShouldNotBeNull().ShouldBe(new Dictionary<long, OrganizationRole> { [organization.Id] = OrganizationRole.Participant });
    // The memberships are refreshed on the lane of the instance from then on.
    _queued.ShouldHaveSingleItem().OrganizationId.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_TokensWithoutExpiry_ShouldStoreNoExpiry()
  {
    _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));

    await _handler.Handle(new SignInUserCommand(AccessToken, null, null, null), Ct);

    var user = _added.ShouldNotBeNull();
    user.AccessTokenExpiresAt.ShouldBeNull();
    user.RefreshTokenProtected.ShouldBeNull();
    user.RefreshTokenExpiresAt.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_KnownAccount_ShouldRefreshItsRow()
  {
    _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    var known = WithId(AppUser.Create(Forge.GitHub, "583231", "old-login", null));
    A.CallTo(() => _users.FindByExternalIdAsync(Forge.GitHub, "583231", A<CancellationToken>._)).Returns(known);

    await HandleAsync();

    known.Login.ShouldBe("octocat");
    known.AccessTokenProtected.ShouldBe($"protected({AccessToken})");
    A.CallTo(() => _users.UpdateAsync(known, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _users.TryAddAsync(A<AppUser>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Two sign-ins of the same account at once: the second one refreshes the row the first one added.
  [Fact]
  public async Task Handle_AccountAddedByAnotherSignIn_ShouldRefreshThatRow()
  {
    _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    var other = WithId(AppUser.Create(Forge.GitHub, "583231", "octocat", null));
    A.CallTo(() => _users.FindByExternalIdAsync(Forge.GitHub, "583231", A<CancellationToken>._)).ReturnsNextFromSequence(null, other);
    A.CallTo(() => _users.TryAddAsync(A<AppUser>._, A<CancellationToken>._)).Returns(false);

    (await HandleAsync()).ShouldBe(UserId);

    other.AccessTokenProtected.ShouldBe($"protected({AccessToken})");
    A.CallTo(() => _users.UpdateAsync(other, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A renewal of the tokens saved the row first: it is read again and saved once more.
  [Fact]
  public async Task Handle_ConcurrentChangeOfTheRow_ShouldReadItAgainOnce()
  {
    _directory.Add("1001", "octo-org", admitted: true);
    Reaches(Installed(42, "1001", "octo-org"));
    var stale = WithId(AppUser.Create(Forge.GitHub, "583231", "octocat", null));
    var fresh = WithId(AppUser.Create(Forge.GitHub, "583231", "octocat", null));
    A.CallTo(() => _users.FindByExternalIdAsync(Forge.GitHub, "583231", A<CancellationToken>._)).ReturnsNextFromSequence(stale, fresh);
    A.CallTo(() => _users.UpdateAsync(stale, A<CancellationToken>._)).Throws(new ConflictException("General.ConcurrentChange", "changed"));

    await HandleAsync();

    fresh.AccessTokenProtected.ShouldBe($"protected({AccessToken})");
    A.CallTo(() => _users.UpdateAsync(fresh, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ConcurrentChangeTwice_ShouldThrowConflict()
  {
    var row = WithId(AppUser.Create(Forge.GitHub, "583231", "octocat", null));
    A.CallTo(() => _users.FindByExternalIdAsync(Forge.GitHub, "583231", A<CancellationToken>._)).Returns(row);
    A.CallTo(() => _users.UpdateAsync(row, A<CancellationToken>._)).Throws(new ConflictException("General.ConcurrentChange", "changed"));

    await Should.ThrowAsync<ConflictException>(async () => await HandleAsync());

    A.CallTo(_memberships).MustNotHaveHappened();
  }

  [Fact]
  public void ToString_ShouldNotContainTheTokens()
  {
    var text = Command.ToString();

    text.ShouldNotContain(AccessToken);
    text.ShouldNotContain(RefreshToken);
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static GitHubInstallationInfo Installed(long id, string accountId, string login) =>
    new(id, accountId, login, OrganizationAccountType.Organization, false);

  private static AppUser WithId(AppUser user)
  {
    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, UserId);

    return user;
  }

  private async Task<long> HandleAsync() => await _handler.Handle(Command, Ct);

  private void Reaches(params GitHubInstallationInfo[] installations)
  {
    A.CallTo(() => _userGateway.ListInstallationsAsync(AccessToken, A<CancellationToken>._)).Returns(installations);
  }

  #endregion
}
