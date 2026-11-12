// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositoriesQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.ListRepositories;

/// <summary>Handler for the <see cref="ListRepositoriesQuery" /> query.</summary>
/// <param name="currentUser">The principal of the scope.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="access">Tells which repositories the user may read.</param>
internal sealed class ListRepositoriesQueryHandler(
  ICurrentUser currentUser,
  IRepositoryRepository repositories,
  IRepositoryAccessService access) : IQueryHandler<ListRepositoriesQuery, IReadOnlyList<TrackedRepository>>
{
  #region Implementation of IQueryHandler<ListRepositoriesQuery,IReadOnlyList<TrackedRepository>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<TrackedRepository>> Handle(ListRepositoriesQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var userId = currentUser.UserId;

    UnauthorizedException.ThrowIfNoUser(userId);

    var readable = await access.ListReadableAsync(userId.Value, cancellationToken).ConfigureAwait(false);
    var active = await repositories.SearchActiveAsync(query.Search, cancellationToken).ConfigureAwait(false);

    // A repository disabled by an administrator stays listed (Codecov API v2 repos?active=): its state tells it apart.
    return [.. active.Where(r => readable.Contains(r.ExternalId) && (query.Active is not { } wanted || (r.DisabledAt is null) == wanted))];
  }

  #endregion
}
