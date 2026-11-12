// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Uploads.Queries.GetUpload;

/// <summary>Handler for the <see cref="GetUploadQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="uploads">The uploads of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
internal sealed class GetUploadQueryHandler(CommitLocator locator, IUploadRepository uploads, ICommitRepository commits) : IQueryHandler<GetUploadQuery, Upload>
{
  #region Implementation of IQueryHandler<GetUploadQuery,Upload>

  /// <inheritdoc />
  public async ValueTask<Upload> Handle(GetUploadQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var upload = await uploads.FindByPublicIdAsync(query.UploadId, cancellationToken).ConfigureAwait(false);
    var commit = upload is null ? null : await commits.FindAsync(upload.CommitId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfUploadNotFound(upload is not null && commit?.RepositoryId == repository.Id);

    // The guard passes only with an upload.
    return upload!;
  }

  #endregion
}
