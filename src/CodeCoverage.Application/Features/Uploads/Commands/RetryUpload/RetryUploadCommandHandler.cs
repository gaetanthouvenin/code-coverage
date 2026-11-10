// ------------------------------------------------------------------------------------------------
// <copyright file="RetryUploadCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Uploads.Commands.RetryUpload;

/// <summary>Handler for the <see cref="RetryUploadCommand" /> command.</summary>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="uploads">The repository of the uploads.</param>
/// <param name="requeuer">What the retry of an upload and the reprocessing of a commit share.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class RetryUploadCommandHandler(
  IRepositoryRepository repositories,
  ICommitRepository commits,
  IUploadRepository uploads,
  UploadRequeuer requeuer,
  IAuditLog auditLog) : ICommandHandler<RetryUploadCommand>
{
  #region Fields

  /// <summary>The action recorded in the audit trail.</summary>
  internal const string AuditAction = "Uploads.Retry";

  #endregion

  #region Implementation of ICommandHandler<RetryUploadCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">
  ///   The repository does not exist, or the upload is not one of this repository: the upload of another
  ///   organization is never read.
  /// </exception>
  /// <exception cref="ConflictException">The upload is not failed, or another request retried it in the meantime.</exception>
  /// <exception cref="UnprocessableEntityException">The raw files of the upload are no longer kept.</exception>
  public async ValueTask<Unit> Handle(RetryUploadCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = RepositorySlug.TryFromAddress(command.Owner, command.Name, out var slug)
                       ? await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false)
                       : null;

    NotFoundException.ThrowIfRepositoryNotFound(repository);

    var upload = await uploads.FindByPublicIdAsync(command.UploadId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfUploadNotFound(upload is not null);

    var commit = await commits.FindAsync(upload.CommitId, cancellationToken).ConfigureAwait(false);

    // The permission was checked on the repository of the address: an upload of another one is not found.
    NotFoundException.ThrowIfUploadNotFound(commit is not null && commit.RepositoryId == repository.Id);
    ConflictException.ThrowIfUploadNotFailed(upload.State);
    UnprocessableEntityException.ThrowIfUploadRawExpired(await requeuer.HasExpiredRawAsync([upload], cancellationToken).ConfigureAwait(false));

    var taken = await requeuer.RequeueAsync([upload], cancellationToken).ConfigureAwait(false);

    // Not taken: another request did between the read and the guarded update, and its job is already queued.
    ConflictException.ThrowIfUploadNotFailed(taken.Count == 0 ? UploadState.Received : UploadState.Failed);

    await auditLog.RecordAsync(AuditAction, upload.PublicId.ToString("D"), null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
