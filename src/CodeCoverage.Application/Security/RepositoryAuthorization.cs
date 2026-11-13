// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryAuthorization.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Security;

/// <summary>
///   The one rule that says what a user may do on a repository: the permission mirrored from the forge, and the
///   administrators of the organization that owns it.
/// </summary>
/// <remarks>
///   <para>
///     An administrator of an organization (<see cref="OrganizationRole.Admin" />) administers the repositories of
///     that organization: the forges give their organization owners the administration of every repository, and the
///     console says the same. The instance administrator gains nothing from it, as the rule reads the role the user
///     holds in that very organization.
///   </para>
///   <para>
///     The rule never answers whether the repository may be read: the caller checks that first, with
///     <see cref="IRepositoryAccessService.ListReadableAsync" /> or
///     <see cref="IRepositoryAccessService.CanAsync" /> at <see cref="RepositoryPermission.Read" />, so that a
///     repository the user cannot read stays the single « not found » of ADR-013.
///   </para>
///   <para>
///     <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" /> and the
///     <c>GetRepositoryPermission</c> query both come here, so that the gate and what the console shows cannot
///     diverge.
///   </para>
/// </remarks>
internal static class RepositoryAuthorization
{
  #region Methods

  /// <summary>Tells whether a user holds a permission on a readable repository of the organization of the scope.</summary>
  /// <param name="permission">The permission to hold.</param>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier of the repository on its forge.</param>
  /// <param name="access">The service of the permissions mirrored from the forge.</param>
  /// <param name="roles">The resolver of the role of the user in the organization.</param>
  /// <param name="organization">The organization of the scope, the one that owns the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reads.</param>
  /// <returns>A task that gives whether the user holds the permission.</returns>
  /// <exception cref="ArgumentNullException">A service or the identifier of the repository is null.</exception>
  internal static async Task<bool> HasPermissionAsync(
    RepositoryPermission permission,
    long userId,
    string repositoryExternalId,
    IRepositoryAccessService access,
    IOrganizationRoleResolver roles,
    ICurrentOrganization organization,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repositoryExternalId);
    ArgumentNullException.ThrowIfNull(access);
    ArgumentNullException.ThrowIfNull(roles);
    ArgumentNullException.ThrowIfNull(organization);

    // Reading is what the caller already proved: nothing more to ask of the forge or of the organization.
    if (permission is RepositoryPermission.Read)
    {
      return true;
    }

    if (await access.CanAsync(userId, repositoryExternalId, permission, cancellationToken).ConfigureAwait(false))
    {
      return true;
    }

    var role = await roles.ResolveAsync(userId, organization.Id, cancellationToken).ConfigureAwait(false);

    return role is OrganizationRole.Admin;
  }

  #endregion
}
