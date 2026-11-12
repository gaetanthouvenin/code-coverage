// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositoriesQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.ListRepositories;

/// <summary>Lists the active repositories of an organization that the signed-in user may read on its forge.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <param name="Search">A text the name must contain, whatever its case, or <see langword="null" />.</param>
/// <param name="Active">
///   <see langword="true" /> for the enabled repositories only, <see langword="false" /> for the disabled ones, or
///   <see langword="null" /> for both.
/// </param>
/// <remarks>A repository the user cannot read is left out, as if it did not exist; the instance administrator sees no more.</remarks>
public sealed record ListRepositoriesQuery(Forge Forge, string Login, string? Search, bool? Active = null) : IQuery<IReadOnlyList<TrackedRepository>>, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Participant;

  #endregion
}
