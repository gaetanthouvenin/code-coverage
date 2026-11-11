// ------------------------------------------------------------------------------------------------
// <copyright file="TriggerNotifyCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Notifications.Commands.TriggerNotify;

/// <summary>Handler for the <see cref="TriggerNotifyCommand" /> command.</summary>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class TriggerNotifyCommandHandler(
  IRepositoryRepository repositories,
  ICommitRepository commits,
  IJobQueue jobQueue,
  IAuditLog auditLog) : ICommandHandler<TriggerNotifyCommand>
{
  #region Fields

  /// <summary>The action recorded in the audit trail.</summary>
  internal const string AuditAction = "Commits.Notify";

  #endregion

  #region Implementation of ICommandHandler<TriggerNotifyCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository or the commit does not exist in the organization of the scope.</exception>
  public async ValueTask<Unit> Handle(TriggerNotifyCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = RepositorySlug.TryFromAddress(command.Owner, command.Name, out var slug)
                       ? await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false)
                       : null;

    NotFoundException.ThrowIfRepositoryNotFound(repository);

    var commit = await commits.FindByShaAsync(repository.Id, command.Sha, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfCommitNotFound(commit);

    // An isolated trigger: no row changes with it. Two requests that wait give one job.
    await jobQueue.EnqueueAsync(NotifyCommitJobs.CreateForced(commit.Id), cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync(AuditAction, $"{repository.Owner}/{repository.Name}@{commit.Sha.Value}", null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
