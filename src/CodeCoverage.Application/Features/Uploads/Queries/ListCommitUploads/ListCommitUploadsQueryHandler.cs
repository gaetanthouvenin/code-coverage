// ------------------------------------------------------------------------------------------------
// <copyright file="ListCommitUploadsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Uploads.Queries.ListCommitUploads;

/// <summary>Handler for the <see cref="ListCommitUploadsQuery" /> query.</summary>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="uploads">The uploads of the organization.</param>
internal sealed class ListCommitUploadsQueryHandler(IRepositoryRepository repositories, ICommitRepository commits, IUploadRepository uploads)
  : IQueryHandler<ListCommitUploadsQuery, IReadOnlyList<Upload>>
{
  #region Implementation of IQueryHandler<ListCommitUploadsQuery,IReadOnlyList<Upload>>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not tracked.</exception>
  public async ValueTask<IReadOnlyList<Upload>> Handle(ListCommitUploadsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = RepositorySlug.TryFromAddress(query.Owner, query.Name, out var slug)
                       ? await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false)
                       : null;

    if (repository is null)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");
    }

    var commit = await commits.FindByShaAsync(repository.Id, query.Sha, cancellationToken).ConfigureAwait(false);

    return commit is null ? [] : await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
