// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositoryPermissionQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetRepositoryPermission;

/// <summary>Handler for the <see cref="GetRepositoryPermissionQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="currentUser">The sender of the query.</param>
/// <param name="access">The permissions mirrored from the forge.</param>
/// <param name="roles">The role of the sender in the organization of the repository.</param>
/// <param name="organization">The organization the gate entered.</param>
/// <remarks>
///   The gate already proved the sender can read the repository, so only the administration is left to answer. A
///   caller that is not a user at all (the system, a token without a user) administers nothing here.
/// </remarks>
internal sealed class GetRepositoryPermissionQueryHandler(
  CommitLocator locator,
  ICurrentUser currentUser,
  IRepositoryAccessService access,
  IOrganizationRoleResolver roles,
  ICurrentOrganization organization) : IQueryHandler<GetRepositoryPermissionQuery, bool>
{
  #region Implementation of IQueryHandler<GetRepositoryPermissionQuery,bool>

  /// <inheritdoc />
  public async ValueTask<bool> Handle(GetRepositoryPermissionQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (currentUser.UserId is not { } userId)
    {
      return false;
    }

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);

    return await RepositoryAuthorization
                 .HasPermissionAsync(RepositoryPermission.Admin, userId, repository.ExternalId, access, roles, organization, cancellationToken)
                 .ConfigureAwait(false);
  }

  #endregion
}
