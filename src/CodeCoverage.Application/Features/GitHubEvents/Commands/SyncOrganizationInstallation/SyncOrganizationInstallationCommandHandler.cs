// ------------------------------------------------------------------------------------------------
// <copyright file="SyncOrganizationInstallationCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.SyncOrganizationInstallation;

/// <summary>Handler for the <see cref="SyncOrganizationInstallationCommand" /> command.</summary>
/// <param name="installations">The installations recorded for the organization.</param>
/// <param name="organization">The organization of the scope.</param>
/// <param name="jobs">The queue of the jobs.</param>
/// <remarks>
///   One job per installation of the organization, in its own lane (<c>JobRequest.Create</c>), with the key the
///   webhooks already use: a reconciliation already waiting absorbs this one.
/// </remarks>
internal sealed class SyncOrganizationInstallationCommandHandler(
  IInstallationRepository installations,
  ICurrentOrganization organization,
  IJobQueue jobs) : ICommandHandler<SyncOrganizationInstallationCommand>
{
  #region Implementation of ICommandHandler<SyncOrganizationInstallationCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncOrganizationInstallationCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var ids = await installations.ListIdsForOrganizationAsync(organization.Id, cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfNoInstallation(ids);

    foreach (var installationId in ids)
    {
      await jobs.TryEnqueueAsync(RepositoryReconciliationJobs.ForInstallation(installationId), cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
