// ------------------------------------------------------------------------------------------------
// <copyright file="CheckInstallationsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Organizations.Commands.CheckInstallations;

/// <summary>Handler for the <see cref="CheckInstallationsCommand" /> command.</summary>
/// <param name="auditLog">The audit trail.</param>
/// <param name="jobQueue">The job queue.</param>
/// <remarks>
///   The request only queues the coordinator of the instance lane (<c>SyncInstallations</c>), merged with the one
///   already waiting: the worker asks GitHub, never the request.
/// </remarks>
internal sealed class CheckInstallationsCommandHandler(IAuditLog auditLog, IJobQueue jobQueue) : ICommandHandler<CheckInstallationsCommand>
{
  #region Implementation of ICommandHandler<CheckInstallationsCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(CheckInstallationsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    await jobQueue.EnqueueAsync(SyncInstallationsJobs.Create(), cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync("Organization.CheckInstallations", "GitHub", null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
