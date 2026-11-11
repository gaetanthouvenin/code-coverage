// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRoles.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Organizations;

/// <summary>Decides the role of a user in an organization from what its forge says.</summary>
public static class OrganizationRoles
{
  #region Methods

  /// <summary>Tells whether a user is the owner of the personal account an organization stands for.</summary>
  /// <param name="user">The user.</param>
  /// <param name="organization">The organization.</param>
  /// <returns>
  ///   <see langword="true" /> when the organization is a personal account of the forge of the user, and its account
  ///   identifier is the one of the user.
  /// </returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static bool Owns(AppUser user, Organization organization)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(organization);

    return organization.AccountType is OrganizationAccountType.User
           && organization.Forge == user.Forge
           && string.Equals(organization.ExternalAccountId, Organization.NormalizeExternalAccountId(user.ExternalId), StringComparison.Ordinal);
  }

  /// <summary>Gives the role of a user that is linked to an organization on GitHub.</summary>
  /// <param name="user">The user.</param>
  /// <param name="organization">The organization.</param>
  /// <param name="membership">What GitHub says of the user in the organization, or <see langword="null" /> when it is not a member.</param>
  /// <returns>
  ///   <see cref="OrganizationRole.Admin" /> for the owner of a personal account, and for an active membership with
  ///   the admin role that describes the account of the user; otherwise <see cref="OrganizationRole.Participant" />: a
  ///   member, an outside collaborator, someone whose invitation is still pending, or a membership GitHub gave for
  ///   another account, which holds the login of the user today.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="user" /> or <paramref name="organization" /> is <see langword="null" />.</exception>
  public static OrganizationRole Of(AppUser user, Organization organization, GitHubOrganizationMembership? membership)
  {
    if (Owns(user, organization))
    {
      return OrganizationRole.Admin;
    }

    return organization.AccountType is OrganizationAccountType.Organization
           && membership is { Active: true, Admin: true }
           && string.Equals(membership.UserExternalId, user.ExternalId, StringComparison.OrdinalIgnoreCase)
             ? OrganizationRole.Admin
             : OrganizationRole.Participant;
  }

  #endregion
}
