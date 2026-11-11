// ------------------------------------------------------------------------------------------------
// <copyright file="IOrganizationMembershipRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>The memberships of the users in the organizations of the instance.</summary>
/// <remarks>
///   The port reads and writes a global table: it serves before a scope enters an organization. Every method names
///   the user: nothing here is filtered by the organization of the scope.
/// </remarks>
public interface IOrganizationMembershipRepository
{
  #region Methods

  /// <summary>Finds the membership of a user in an organization, for reading only.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The membership, whatever its age, or <see langword="null" />.</returns>
  Task<OrganizationMembership?> FindAsync(long userId, long organizationId, CancellationToken cancellationToken);

  /// <summary>Lists the memberships of a user, for reading only.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The memberships, whatever their age, by organization.</returns>
  Task<IReadOnlyList<OrganizationMembership>> ListOfUserAsync(long userId, CancellationToken cancellationToken);

  /// <summary>Replaces the memberships of a user with what the forge says now, saved at once.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="roles">The role of the user in each organization it is linked to, by organization identifier.</param>
  /// <param name="at">When the forge was asked.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the memberships are saved.</returns>
  /// <remarks>
  ///   In one save: a membership that is listed is created or refreshed, one that is not is removed. Refused as a
  ///   conflict (<c>General.ConcurrentChange</c>) when another refresh of the same user wrote meanwhile: nothing of
  ///   this one is kept.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="roles" /> is <see langword="null" />.</exception>
  Task ReplaceAsync(long userId, IReadOnlyDictionary<long, OrganizationRole> roles, DateTimeOffset at, CancellationToken cancellationToken);

  #endregion
}
