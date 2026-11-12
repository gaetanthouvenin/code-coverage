// ------------------------------------------------------------------------------------------------
// <copyright file="ListMyOrganizationsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;

/// <summary>Handler for the <see cref="ListMyOrganizationsQuery" /> query.</summary>
/// <param name="currentUser">The principal that asks.</param>
/// <param name="memberships">The repository of the memberships.</param>
/// <param name="roles">What tells the role of a user in an organization, failing closed.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <remarks>
///   Only the memberships of the principal are read, and each one goes through the role resolver: an organization
///   whose membership is too old, or that is not active, is not listed.
/// </remarks>
internal sealed class ListMyOrganizationsQueryHandler(
  ICurrentUser currentUser,
  IOrganizationMembershipRepository memberships,
  IOrganizationRoleResolver roles,
  IOrganizationDirectory directory) : IQueryHandler<ListMyOrganizationsQuery, IReadOnlyList<MemberOrganization>>
{
  #region Implementation of IQueryHandler<ListMyOrganizationsQuery,IReadOnlyList<MemberOrganization>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<MemberOrganization>> Handle(ListMyOrganizationsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    // A principal without a user row, the system for instance, is linked to nothing.
    if (currentUser.UserId is not { } userId)
    {
      return [];
    }

    var organizations = new List<MemberOrganization>();

    foreach (var membership in await memberships.ListOfUserAsync(userId, cancellationToken).ConfigureAwait(false))
    {
      // A token of an organization stays within it (M3.05): the other organizations of its user are not listed.
      if (currentUser.TokenOrganizationId is { } tokenOrganization && tokenOrganization != membership.OrganizationId)
      {
        continue;
      }

      if (await roles.ResolveAsync(userId, membership.OrganizationId, cancellationToken).ConfigureAwait(false) is { } role
          && await directory.FindAsync(membership.OrganizationId, cancellationToken).ConfigureAwait(false) is { State: OrganizationState.Active } organization)
      {
        organizations.Add(new MemberOrganization(organization, role));
      }
    }

    return [.. organizations.OrderBy(static o => o.Organization.NormalizedLogin, StringComparer.Ordinal).ThenBy(static o => o.Organization.Id)];
  }

  #endregion
}
