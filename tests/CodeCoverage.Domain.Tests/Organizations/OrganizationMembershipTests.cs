// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationMembershipTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Tests.Organizations;

public sealed class OrganizationMembershipTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private static readonly TimeSpan HardExpiry = TimeSpan.FromMinutes(60);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldKeepTheLinkTheRoleAndWhenItWasRead()
  {
    var membership = OrganizationMembership.Create(7, 9, OrganizationRole.Admin, Now.ToOffset(TimeSpan.FromHours(2)));

    membership.UserId.ShouldBe(7);
    membership.OrganizationId.ShouldBe(9);
    membership.Role.ShouldBe(OrganizationRole.Admin);
    membership.RefreshedAt.ShouldBe(Now);
    membership.RefreshedAt.Offset.ShouldBe(TimeSpan.Zero);
  }

  [Fact]
  public void Refresh_ShouldReplaceTheRoleAndTheDate()
  {
    var membership = OrganizationMembership.Create(7, 9, OrganizationRole.Admin, Now);

    membership.Refresh(OrganizationRole.Participant, Now.AddMinutes(30));

    membership.Role.ShouldBe(OrganizationRole.Participant);
    membership.RefreshedAt.ShouldBe(Now.AddMinutes(30));
  }

  [Theory]
  [InlineData(0, true)]
  [InlineData(59, true)]
  [InlineData(60, false)]
  [InlineData(61, false)]
  [InlineData(100_000, false)]
  // A date in the future was written by no clock that agrees with this one: it grants nothing.
  [InlineData(-1, false)]
  public void IsFresh_ShouldGrantNothingOnceTheHardExpiryHasPassed(int minutesSinceTheRefresh, bool fresh)
  {
    var membership = OrganizationMembership.Create(7, 9, OrganizationRole.Admin, Now);

    membership.IsFresh(Now.AddMinutes(minutesSinceTheRefresh), HardExpiry).ShouldBe(fresh);
  }

  // F13. The Api writes the date, the Web reads it with its own clock: a few seconds ahead is a skew, not a forgery.
  [Theory]
  [InlineData(-1, true)]
  [InlineData(-30, true)]
  [InlineData(-31, false)]
  [InlineData(-3600, false)]
  public void IsFresh_RefreshSlightlyAheadOfTheClock_ShouldTolerateTheSkew(int secondsSinceTheRefresh, bool fresh)
  {
    var membership = OrganizationMembership.Create(7, 9, OrganizationRole.Admin, Now);

    membership.IsFresh(Now.AddSeconds(secondsSinceTheRefresh), HardExpiry).ShouldBe(fresh);
  }

  // S9. A hard expiry of zero, or below, grants nothing, a refresh dated slightly ahead included.
  [Theory]
  [InlineData(0)]
  [InlineData(-5)]
  public void IsFresh_HardExpiryOfZeroOrBelow_ShouldGrantNothingEvenSlightlyAhead(int minutes)
  {
    var membership = OrganizationMembership.Create(7, 9, OrganizationRole.Admin, Now);

    membership.IsFresh(Now.AddSeconds(-10), TimeSpan.FromMinutes(minutes)).ShouldBeFalse();
    membership.IsFresh(Now, TimeSpan.FromMinutes(minutes)).ShouldBeFalse();
  }

  [Fact]
  public void IsFresh_WithoutAnyAllowance_ShouldNeverBeFresh()
  {
    OrganizationMembership.Create(7, 9, OrganizationRole.Admin, Now).IsFresh(Now, TimeSpan.Zero).ShouldBeFalse();
  }

  [Fact]
  public void Create_InvalidArguments_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => OrganizationMembership.Create(0, 9, OrganizationRole.Admin, Now));
    Should.Throw<ArgumentOutOfRangeException>(() => OrganizationMembership.Create(7, 0, OrganizationRole.Admin, Now));
    Should.Throw<ArgumentOutOfRangeException>(() => OrganizationMembership.Create(7, 9, (OrganizationRole)42, Now));
    Should.Throw<ArgumentOutOfRangeException>(() => OrganizationMembership.Create(7, 9, OrganizationRole.Admin, Now).Refresh((OrganizationRole)42, Now));
  }

  // Admin only for an active membership with the admin role, or for the owner of a personal account.
  [Theory]
  [InlineData(true, true, OrganizationRole.Admin)]
  [InlineData(true, false, OrganizationRole.Participant)]
  // An invitation that was not accepted yet, even to own the organization.
  [InlineData(false, true, OrganizationRole.Participant)]
  [InlineData(false, false, OrganizationRole.Participant)]
  public void Of_Organization_ShouldBeAdminOnlyForAnActiveAdminMembership(bool active, bool admin, OrganizationRole expected)
  {
    var organization = Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now);

    OrganizationRoles.Of(User("583231"), organization, new GitHubOrganizationMembership(active, admin, "583231")).ShouldBe(expected);
  }

  // F11. The membership is read by login: a user renamed on GitHub, whose former login an owner of the organization
  // took, must not get that owner's role.
  [Theory]
  [InlineData("999")]
  [InlineData(null)]
  public void Of_MembershipOfAnotherAccount_ShouldGrantNothing(string? describedAccount)
  {
    var organization = Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now);

    OrganizationRoles.Of(User("583231"), organization, new GitHubOrganizationMembership(true, true, describedAccount)).ShouldBe(OrganizationRole.Participant);
  }

  // An outside collaborator of a repository is not a member of the organization.
  [Fact]
  public void Of_OrganizationWithoutMembership_ShouldBeParticipant()
  {
    var organization = Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now);

    OrganizationRoles.Of(User("583231"), organization, null).ShouldBe(OrganizationRole.Participant);
  }

  [Fact]
  public void Of_PersonalAccount_ShouldBeAdminForItsOwnerOnly()
  {
    var account = Organization.CreatePending(Forge.GitHub, "583231", "octocat", OrganizationAccountType.User, Now);

    OrganizationRoles.Of(User("583231"), account, null).ShouldBe(OrganizationRole.Admin);
    OrganizationRoles.Of(User("999"), account, null).ShouldBe(OrganizationRole.Participant);
    // What GitHub would say of a membership means nothing for a personal account.
    OrganizationRoles.Of(User("999"), account, new GitHubOrganizationMembership(true, true, "999")).ShouldBe(OrganizationRole.Participant);
  }

  [Fact]
  public void Owns_ShouldCompareTheAccountOnTheSameForgeWhateverTheCase()
  {
    var bitbucket = Organization.CreatePending(Forge.Bitbucket, "{ABC}", "someone", OrganizationAccountType.User, Now);
    var organization = Organization.CreatePending(Forge.GitHub, "583231", "octo-org", OrganizationAccountType.Organization, Now);

    OrganizationRoles.Owns(AppUser.Create(Forge.Bitbucket, "{abc}", "someone", null), bitbucket).ShouldBeTrue();
    // The same identifier on another forge is another account.
    OrganizationRoles.Owns(AppUser.Create(Forge.GitHub, "{abc}", "someone", null), bitbucket).ShouldBeFalse();
    // An organization is owned by nobody, even by a user with its identifier.
    OrganizationRoles.Owns(User("583231"), organization).ShouldBeFalse();
    Should.Throw<ArgumentNullException>(() => OrganizationRoles.Owns(null!, organization));
    Should.Throw<ArgumentNullException>(() => OrganizationRoles.Owns(User("1"), null!));
  }

  [Fact]
  public void RefreshJob_ShouldRunOnTheLaneOfTheInstanceMergedAndLockedPerUser()
  {
    var job = RefreshUserMembershipsJobs.Create(7);

    job.Type.ShouldBe(JobTypes.RefreshUserMemberships);
    job.IsInstance.ShouldBeTrue();
    job.OrganizationId.ShouldBeNull();
    job.PayloadJson.ShouldBe("""{"UserId":7}""");
    job.LockKey.ShouldBe("memberships:7");
    job.DedupKey.ShouldBe("memberships:7");
    job.Coalesce.ShouldBeTrue();
    RefreshUserMembershipsJobs.Create(8).DedupKey.ShouldNotBe(job.DedupKey);
    Should.Throw<ArgumentOutOfRangeException>(() => RefreshUserMembershipsJobs.Create(0));
  }

  private static AppUser User(string externalId) => AppUser.Create(Forge.GitHub, externalId, "octocat", null);

  #endregion
}
