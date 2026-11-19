// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRolesTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Tests.Organizations;

public sealed class OrganizationRolesTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  // The role is read from the neutral membership, whatever the forge of the organization.
  [Theory]
  [InlineData(Forge.GitHub, "583231", "583231")]
  [InlineData(Forge.GitLab, "77", "77")]
  [InlineData(Forge.Bitbucket, "{abc-123}", "{ABC-123}")]
  public void Of_AdminMembership_ShouldBeAdmin(Forge forge, string userExternalId, string describedUser)
  {
    var organization = Organization.CreatePending(forge, "1001", "octo-org", OrganizationAccountType.Organization, Now);
    var user = AppUser.Create(forge, userExternalId, "octocat", null);

    OrganizationRoles.Of(user, organization, new ForgeMembership(true, true, describedUser)).ShouldBe(OrganizationRole.Admin);
  }

  [Fact]
  public void Of_MemberMembership_ShouldBeParticipant()
  {
    var organization = Organization.CreatePending(Forge.GitLab, "1001", "octo-group", OrganizationAccountType.Organization, Now);
    var user = AppUser.Create(Forge.GitLab, "77", "octocat", null);

    OrganizationRoles.Of(user, organization, new ForgeMembership(true, false, "77")).ShouldBe(OrganizationRole.Participant);
  }

  #endregion
}
