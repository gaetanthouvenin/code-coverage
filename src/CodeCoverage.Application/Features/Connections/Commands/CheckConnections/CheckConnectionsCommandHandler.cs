// ------------------------------------------------------------------------------------------------
// <copyright file="CheckConnectionsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Connections.Commands.CheckConnections;

/// <summary>Handler for the <see cref="CheckConnectionsCommand" /> command.</summary>
/// <param name="connections">The repository of the forge connections.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="jobQueue">The job queue.</param>
/// <remarks>
///   A coordinator (ADR-013, ADR-017): it reads only global tables, <c>ForgeConnections</c> and <c>Organizations</c>, and
///   queues, by the identifier of its organization, the check of each connection that is not suspended and whose
///   organization is active. Each check is a job of its own: one connection that fails never fails the check of another.
/// </remarks>
internal sealed class CheckConnectionsCommandHandler(
  IForgeConnectionRepository connections,
  IOrganizationDirectory directory,
  IJobQueue jobQueue) : ICommandHandler<CheckConnectionsCommand>
{
  #region Implementation of ICommandHandler<CheckConnectionsCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(CheckConnectionsCommand command, CancellationToken cancellationToken)
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
      var key = ConnectionHealthJobs.CheckKey(connection.Id);
      var job = JobRequest.ForOrganization(
        connection.OrganizationId,
        JobTypes.CheckConnection,
        ConnectionHealthJobs.CheckPayload(connection.Id),
        key,
        key,
        coalesce: true
      );

      await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
