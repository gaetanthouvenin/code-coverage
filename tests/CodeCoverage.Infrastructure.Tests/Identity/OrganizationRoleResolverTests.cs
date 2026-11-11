// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRoleResolverTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure.Tests.Identity;

public sealed class OrganizationRoleResolverTests
{
  #region Fields

  private const long UserId = 7;

  private const long OrganizationId = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private readonly IOrganizationMembershipRepository _memberships = A.Fake<IOrganizationMembershipRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IUserRepository _users = A.Fake<IUserRepository>();

  private readonly AccessOptions _options = new();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly OrganizationRoleResolver _resolver;

  #endregion

  #region Ctors

  public OrganizationRoleResolverTests()
  {
    var monitor = A.Fake<IOptionsMonitor<AccessOptions>>();

    A.CallTo(() => monitor.CurrentValue).Returns(_options);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _memberships.FindAsync(A<long>._, A<long>._, A<CancellationToken>._)).Returns((OrganizationMembership?)null);
    A.CallTo(() => _directory.FindAsync(A<long>._, A<CancellationToken>._)).Returns((Organization?)null);
    A.CallTo(() => _users.FindAsync(A<long>._, A<CancellationToken>._)).Returns((AppUser?)null);

    _resolver = new OrganizationRoleResolver(_memberships, _directory, _users, monitor, _clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [InlineData(OrganizationRole.Admin)]
  [InlineData(OrganizationRole.Participant)]
  public async Task Resolve_FreshMembershipOfAnActiveOrganization_ShouldGiveItsRole(OrganizationRole role)
  {
    Given(Active("1001", OrganizationAccountType.Organization), role, Now.AddMinutes(-59));

    (await ResolveAsync()).ShouldBe(role);
  }

  // Fail closed: the membership could not be refreshed, because its job died or the user no longer has a token.
  [Theory]
  [InlineData(60)]
  [InlineData(61)]
  [InlineData(100_000)]
  public async Task Resolve_PastHardExpiry_ShouldGrantNothing(int minutes)
  {
    Given(Active("1001", OrganizationAccountType.Organization), OrganizationRole.Admin, Now.AddMinutes(-minutes));

    (await ResolveAsync()).ShouldBeNull();
  }

  // The expiry is a setting read at each call: shortening it takes effect on the next request, without a restart.
  [Fact]
  public async Task Resolve_ShouldReadTheHardExpiryAtEachCall()
  {
    Given(Active("1001", OrganizationAccountType.Organization), OrganizationRole.Admin, Now.AddMinutes(-30));

    (await ResolveAsync()).ShouldBe(OrganizationRole.Admin);

    _options.HardExpiryMinutes = 30;
    (await ResolveAsync()).ShouldBeNull();

    _options.HardExpiryMinutes = 0;
    A.CallTo(() => _clock.UtcNow).Returns(Now.AddMinutes(-30));
    (await ResolveAsync()).ShouldBeNull();

    _options.HardExpiryMinutes = -5;
    (await ResolveAsync()).ShouldBeNull();
  }

  // Nothing is kept between two calls: a row that was removed stops granting at once.
  [Fact]
  public async Task Resolve_ShouldCacheNothing()
  {
    Given(Active("1001", OrganizationAccountType.Organization), OrganizationRole.Admin, Now);
    (await ResolveAsync()).ShouldBe(OrganizationRole.Admin);

    A.CallTo(() => _memberships.FindAsync(UserId, OrganizationId, A<CancellationToken>._)).Returns((OrganizationMembership?)null);

    (await ResolveAsync()).ShouldBeNull();
    A.CallTo(() => _memberships.FindAsync(UserId, OrganizationId, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public async Task Resolve_PersonalAccountOwner_ShouldBeAdmin()
  {
    // Stored as a participant, by a refresh older than the rule or by a mistake: the owner still administers.
    Given(Active("583231", OrganizationAccountType.User), OrganizationRole.Participant, Now);
    A.CallTo(() => _users.FindAsync(UserId, A<CancellationToken>._)).Returns(AppUser.Create(Forge.GitHub, "583231", "octocat", null));

    (await ResolveAsync()).ShouldBe(OrganizationRole.Admin);
  }

  [Fact]
  public async Task Resolve_PersonalAccountOfSomeoneElse_ShouldGiveTheStoredRole()
  {
    Given(Active("583231", OrganizationAccountType.User), OrganizationRole.Participant, Now);
    A.CallTo(() => _users.FindAsync(UserId, A<CancellationToken>._)).Returns(AppUser.Create(Forge.GitHub, "999", "someone", null));

    (await ResolveAsync()).ShouldBe(OrganizationRole.Participant);
  }

  // Owning the account does not replace the link: without a fresh row, even the owner gets nothing.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Resolve_PersonalAccountOwnerWithoutAFreshMembership_ShouldGrantNothing(bool stale)
  {
    Given(Active("583231", OrganizationAccountType.User), OrganizationRole.Admin, Now.AddMinutes(-61));
    A.CallTo(() => _users.FindAsync(UserId, A<CancellationToken>._)).Returns(AppUser.Create(Forge.GitHub, "583231", "octocat", null));

    if (!stale)
    {
      A.CallTo(() => _memberships.FindAsync(UserId, OrganizationId, A<CancellationToken>._)).Returns((OrganizationMembership?)null);
    }

    (await ResolveAsync()).ShouldBeNull();
  }

  [Theory]
  [InlineData(OrganizationState.Pending)]
  [InlineData(OrganizationState.Suspended)]
  public async Task Resolve_OrganizationThatIsNotActive_ShouldGrantNothing(OrganizationState state)
  {
    var organization = Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now);

    if (state is OrganizationState.Suspended)
    {
      organization.Admit("tests", Now);
      organization.Suspend(SuspensionReason.Administrator, Now);
    }

    organization.State.ShouldBe(state);
    Given(organization, OrganizationRole.Admin, Now);

    (await ResolveAsync()).ShouldBeNull();
  }

  [Fact]
  public async Task Resolve_UnknownOrganizationOrMembership_ShouldGrantNothing()
  {
    (await ResolveAsync()).ShouldBeNull();

    A.CallTo(() => _memberships.FindAsync(UserId, OrganizationId, A<CancellationToken>._)).Returns(OrganizationMembership.Create(UserId, OrganizationId, OrganizationRole.Admin, Now));

    (await ResolveAsync()).ShouldBeNull();
  }

  private static Organization Active(string accountId, OrganizationAccountType accountType)
  {
    var organization = Organization.CreatePending(Forge.GitHub, accountId, "octo-org", accountType, Now);

    organization.Admit("tests", Now);

    return organization;
  }

  private void Given(Organization organization, OrganizationRole role, DateTimeOffset refreshedAt)
  {
    A.CallTo(() => _memberships.FindAsync(UserId, OrganizationId, A<CancellationToken>._)).Returns(OrganizationMembership.Create(UserId, OrganizationId, role, refreshedAt));
    A.CallTo(() => _directory.FindAsync(OrganizationId, A<CancellationToken>._)).Returns(organization);
  }

  private async Task<OrganizationRole?> ResolveAsync() => await _resolver.ResolveAsync(UserId, OrganizationId, Ct);

  #endregion
}
