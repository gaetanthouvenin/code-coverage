// ------------------------------------------------------------------------------------------------
// <copyright file="SyncOrganizationConnectionCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Connections.Commands.SyncOrganizationConnection;

/// <summary>Handler for the <see cref="SyncOrganizationConnectionCommand" /> command.</summary>
/// <param name="installations">The installations recorded for the organizations of GitHub.</param>
/// <param name="connections">The connections of the organizations of the other forges.</param>
/// <param name="organization">The organization of the scope.</param>
/// <param name="jobs">The queue of the jobs.</param>
/// <remarks>
///   One job per installation of the organization on GitHub, with the key <c>installation:{id}</c> the webhooks
///   already use, and one per connection that is not suspended on the other forges, with the key
///   <c>connection:{id}</c> of the periodic run: a reconciliation already waiting absorbs this one. Without either,
///   the refusal is explicit, never a silent no-op.
/// </remarks>
internal sealed class SyncOrganizationConnectionCommandHandler(
  IInstallationRepository installations,
  IForgeConnectionRepository connections,
  ICurrentOrganization organization,
  IJobQueue jobs) : ICommandHandler<SyncOrganizationConnectionCommand>
{
  #region Methods

  /// <summary>Builds the reconciliation of each connection of the organization that is not suspended.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The jobs, one per connection.</returns>
  private async Task<IReadOnlyList<JobRequest>> ForConnectionsAsync(CancellationToken cancellationToken)
  {
    var listed = await connections.ListForOrganizationAsync(organization.Id, cancellationToken).ConfigureAwait(false);

    return
    [
      .. listed.Where(static c => c.Status is not ForgeConnectionStatus.Suspended)
               .Select(static c => RepositoryReconciliationJobs.ForConnection(c.Forge, c.Id)),
    ];
  }

  /// <summary>Builds the reconciliation of each installation of the GitHub App that serves the organization.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The jobs, one per installation.</returns>
  private async Task<IReadOnlyList<JobRequest>> ForInstallationsAsync(CancellationToken cancellationToken)
  {
    var ids = await installations.ListIdsForOrganizationAsync(organization.Id, cancellationToken).ConfigureAwait(false);

    return [.. ids.Select(RepositoryReconciliationJobs.ForInstallation)];
  }

  #endregion

  #region Implementation of ICommandHandler<SyncOrganizationConnectionCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncOrganizationConnectionCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var reconciliations = command.Forge is Forge.GitHub
                            ? await ForInstallationsAsync(cancellationToken).ConfigureAwait(false)
                            : await ForConnectionsAsync(cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfNotConnected(reconciliations, command.Forge);

    foreach (var job in reconciliations)
    {
      await jobs.TryEnqueueAsync(job, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
