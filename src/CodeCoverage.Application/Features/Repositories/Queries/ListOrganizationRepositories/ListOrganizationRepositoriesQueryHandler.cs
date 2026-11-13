// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationRepositoriesQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.ListOrganizationRepositories;

/// <summary>Handler for the <see cref="ListOrganizationRepositoriesQuery" /> query.</summary>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="commits">The commits of the organization, for the last upload of each repository.</param>
/// <remarks>
///   Two reads only: the rows of the organization, then the last upload of each in one query. A repository that left
///   the installation (<see cref="TrackedRepository.IsActive" /> false) is dropped, since nothing but the
///   installation can bring it back.
/// </remarks>
internal sealed class ListOrganizationRepositoriesQueryHandler(
  IRepositoryRepository repositories,
  ICommitRepository commits) : IQueryHandler<ListOrganizationRepositoriesQuery, IReadOnlyList<OrganizationRepository>>
{
  #region Implementation of IQueryHandler<ListOrganizationRepositoriesQuery,IReadOnlyList<OrganizationRepository>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<OrganizationRepository>> Handle(ListOrganizationRepositoriesQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var all = await repositories.ListAsync(cancellationToken).ConfigureAwait(false);
    List<TrackedRepository> served = [.. all.Where(static r => r.IsActive)];
    var uploads = await commits.ListLastUploadsAsync([.. served.Select(static r => r.Id)], cancellationToken).ConfigureAwait(false);

    return
    [
      .. served.OrderBy(static r => r.Name, StringComparer.OrdinalIgnoreCase)
               .Select(r => new OrganizationRepository(r, uploads.TryGetValue(r.Id, out var at) ? at : null)),
    ];
  }

  #endregion
}
