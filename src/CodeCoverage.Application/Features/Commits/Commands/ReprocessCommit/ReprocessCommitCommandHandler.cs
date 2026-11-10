// ------------------------------------------------------------------------------------------------
// <copyright file="ReprocessCommitCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Commits.Commands.ReprocessCommit;

/// <summary>Handler for the <see cref="ReprocessCommitCommand" /> command.</summary>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="uploads">The repository of the uploads.</param>
/// <param name="requeuer">What the retry of an upload and the reprocessing of a commit share.</param>
/// <param name="jobQueue">The queue of the jobs.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class ReprocessCommitCommandHandler(
  IRepositoryRepository repositories,
  ICommitRepository commits,
  IUploadRepository uploads,
  UploadRequeuer requeuer,
  IJobQueue jobQueue,
  IAuditLog auditLog) : ICommandHandler<ReprocessCommitCommand>
{
  #region Fields

  /// <summary>The action recorded in the audit trail.</summary>
  internal const string AuditAction = "Commits.Reprocess";

  #endregion

  #region Implementation of ICommandHandler<ReprocessCommitCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository or the commit does not exist in the organization of the scope.</exception>
  /// <exception cref="UnprocessableEntityException">The commit has no upload, or the raw files of one of them are no longer kept.</exception>
  public async ValueTask<Unit> Handle(ReprocessCommitCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = RepositorySlug.TryFromAddress(command.Owner, command.Name, out var slug)
                       ? await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false)
                       : null;

    NotFoundException.ThrowIfRepositoryNotFound(repository);

    var commit = await commits.FindByShaAsync(repository.Id, command.Sha, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfCommitNotFound(commit);

    var all = await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfCommitNoUpload(all);

    // Refused here, where the caller reads the answer: the job checks again, as the retention goes on meanwhile.
    UnprocessableEntityException.ThrowIfCommitRawExpired(await requeuer.HasExpiredRawAsync(all, cancellationToken).ConfigureAwait(false));

    // An isolated trigger: no row changes with it. Two requests that wait give one job.
    await jobQueue.EnqueueAsync(ReprocessCommitJobs.Create(commit.Id), cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync(AuditAction, $"{repository.Owner}/{repository.Name}@{commit.Sha.Value}", null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
