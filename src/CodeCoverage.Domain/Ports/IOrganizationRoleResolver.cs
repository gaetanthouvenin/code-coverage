// ------------------------------------------------------------------------------------------------
// <copyright file="IOrganizationRoleResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Tells the role a user holds in an organization, failing closed.</summary>
/// <remarks>
///   The answer is no role whenever it cannot be proved now: no membership, a membership older than
///   <c>Access:HardExpiryMinutes</c>, or an organization that is not active. It is never cached: a change of the
///   setting, or the removal of a membership, applies at the next call.
/// </remarks>
public interface IOrganizationRoleResolver
{
  #region Methods

  /// <summary>Tells the role of a user in an organization.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The role, or <see langword="null" /> when the user holds none.</returns>
  Task<OrganizationRole?> ResolveAsync(long userId, long organizationId, CancellationToken cancellationToken);

  #endregion
}
