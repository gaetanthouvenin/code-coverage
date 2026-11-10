// ------------------------------------------------------------------------------------------------
// <copyright file="RequeueCommitUploadsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Commits.Commands.RequeueCommitUploads;

/// <summary>Handler for the <see cref="RequeueCommitUploadsCommand" /> command.</summary>
/// <param name="commits">The repository of the commits.</param>
/// <param name="uploads">The repository of the uploads.</param>
/// <param name="requeuer">What the retry of an upload and the reprocessing of a commit share.</param>
internal sealed class RequeueCommitUploadsCommandHandler(ICommitRepository commits, IUploadRepository uploads, UploadRequeuer requeuer)
  : ICommandHandler<RequeueCommitUploadsCommand>
{
  #region Implementation of ICommandHandler<RequeueCommitUploadsCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The commit no longer exists.</exception>
  /// <exception cref="UnprocessableEntityException">The raw files of an ended upload are no longer kept: nothing is put back in the queue.</exception>
  public async ValueTask<Unit> Handle(RequeueCommitUploadsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var commit = await commits.FindAsync(command.CommitId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfCommitNotFound(commit);

    var all = await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);

    // An upload received or being processed already has its job: it is parsed with the current parsers anyway.
    Upload[] ended = [.. all.Where(UploadRequeuer.HasEnded)];

    if (ended.Length == 0)
    {
      return Unit.Value;
    }

    // All or nothing: a commit rebuilt from a part of its uploads would lose coverage without a word.
    UnprocessableEntityException.ThrowIfCommitRawExpired(await requeuer.HasExpiredRawAsync(ended, cancellationToken).ConfigureAwait(false));

    await requeuer.RequeueAsync(ended, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
