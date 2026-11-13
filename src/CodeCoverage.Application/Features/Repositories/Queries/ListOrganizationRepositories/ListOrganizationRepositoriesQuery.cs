// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationRepositoriesQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.ListOrganizationRepositories;

/// <summary>Lists every repository the installation of an organization serves, with its last upload.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <remarks>
///   Reserved to the administrators of the organization: it lists repositories a member may not read, which
///   <c>ListRepositorySummaries</c> (a participant, filtered by what the user reads) never does. A repository that
///   left the installation is not listed: only the installation brings it back.
/// </remarks>
public sealed record ListOrganizationRepositoriesQuery(Forge Forge, string Login) : IQuery<IReadOnlyList<OrganizationRepository>>, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
