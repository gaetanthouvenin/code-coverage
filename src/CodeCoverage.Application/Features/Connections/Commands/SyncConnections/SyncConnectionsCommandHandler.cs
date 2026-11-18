// ------------------------------------------------------------------------------------------------
// <copyright file="SyncConnectionsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Connections.Commands.SyncConnections;

/// <summary>Handler for the <see cref="SyncConnectionsCommand" /> command.</summary>
/// <param name="connections">The repository of the forge connections.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="jobQueue">The job queue.</param>
/// <remarks>
///   A coordinator (ADR-013, ADR-017): it reads only global tables, <c>ForgeConnections</c> and <c>Organizations</c>, and
///   queues, by the identifier of its organization, the reconciliation of the repositories of each connection that is
///   not suspended and whose organization is active. A connection of an organization that is not active gets no job, so
///   none of its repositories is created.
/// </remarks>
internal sealed class SyncConnectionsCommandHandler(
  IForgeConnectionRepository connections,
  IOrganizationDirectory directory,
  IJobQueue jobQueue) : ICommandHandler<SyncConnectionsCommand>
{
  #region Implementation of ICommandHandler<SyncConnectionsCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncConnectionsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var listed = await connections.ListActiveAsync(cancellationToken).ConfigureAwait(false);

    if (listed.Count == 0)
    {
      return Unit.Value;
    }

    var active = (await directory.ListActiveAsync(cancellationToken).ConfigureAwait(false)).Select(static o => o.Id).ToHashSet();

    // The listing already leaves the suspended connections out; the coordinator does not rely on it alone.
    foreach (var connection in listed.Where(c => c.Status is not ForgeConnectionStatus.Suspended && active.Contains(c.OrganizationId)))
    {
      var key = RepositoryReconciliationJobs.ConnectionKey(connection.Id);
      var job = JobRequest.ForOrganization(
        connection.OrganizationId,
        JobTypes.SyncConnectionRepositories,
        RepositoryReconciliationJobs.ConnectionPayload(connection.Forge, connection.Id),
        key,
        $"sync:{key}",
        coalesce: true
      );

      await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
