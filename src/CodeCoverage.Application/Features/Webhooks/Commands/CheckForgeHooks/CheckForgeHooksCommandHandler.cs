// ------------------------------------------------------------------------------------------------
// <copyright file="CheckForgeHooksCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Webhooks.Commands.CheckForgeHooks;

/// <summary>Handler for the <see cref="CheckForgeHooksCommand" /> command.</summary>
/// <param name="connections">The repository of the forge connections.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="jobQueue">The job queue.</param>
/// <remarks>
///   A coordinator (ADR-013, ADR-017): it reads only global tables, <c>ForgeConnections</c> and <c>Organizations</c>, and
///   queues, by the identifier of its organization, the ensuring of the hooks of each connection that is not suspended
///   and whose organization is active. Each connection is a job of its own: a forge that fails never fails the hooks of
///   another connection.
/// </remarks>
internal sealed class CheckForgeHooksCommandHandler(
  IForgeConnectionRepository connections,
  IOrganizationDirectory directory,
  IJobQueue jobQueue) : ICommandHandler<CheckForgeHooksCommand>
{
  #region Implementation of ICommandHandler<CheckForgeHooksCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(CheckForgeHooksCommand command, CancellationToken cancellationToken)
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
      var key = ForgeHookJobs.EnsureKey(connection.Id);
      var job = JobRequest.ForOrganization(
        connection.OrganizationId,
        JobTypes.EnsureForgeHooks,
        ForgeHookJobs.EnsurePayload(connection.Id),
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
