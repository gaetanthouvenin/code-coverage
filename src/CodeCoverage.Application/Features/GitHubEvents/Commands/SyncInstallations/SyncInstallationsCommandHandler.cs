// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallations;

/// <summary>Handler for the <see cref="SyncInstallationsCommand" /> command.</summary>
/// <param name="gitHub">The gateway of the App.</param>
/// <param name="installations">The repository of the installations.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   A coordinator (ADR-013): it reads only global tables, <c>Installations</c> and <c>Organizations</c>, and
///   queues one job per active organization, by its identifier. An organization that is not active gets no
///   job, so none of its repositories is created.
/// </remarks>
internal sealed class SyncInstallationsCommandHandler(
  IGitHubAppGateway gitHub,
  IInstallationRepository installations,
  IOrganizationDirectory directory,
  IJobQueue jobQueue,
  IClock clock) : ICommandHandler<SyncInstallationsCommand>
{
  #region Methods

  /// <summary>Queues the closing of a transferred repository in the organization it left, when that one is active.</summary>
  /// <param name="repositoryExternalId">The identifier of the repository on GitHub.</param>
  /// <param name="previousOwnerAccountId">The identifier of the account the repository left.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the job is queued, or when there is none to queue.</returns>
  private async Task QueueTransferAsync(string repositoryExternalId, string previousOwnerAccountId, CancellationToken cancellationToken)
  {
    var origin = await directory.FindByAccountIdAsync(Forge.GitHub, previousOwnerAccountId, cancellationToken).ConfigureAwait(false);

    if (origin is not { State: OrganizationState.Active })
    {
      return;
    }

    var job = JobRequest.ForOrganization(
      origin.Id,
      JobTypes.ReconcileRepositoryTransfer,
      RepositoryReconciliationJobs.TransferPayload(repositoryExternalId),
      RepositoryReconciliationJobs.RepositoryKey(repositoryExternalId),
      $"transfer:{repositoryExternalId}",
      coalesce: true
    );

    await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Finds the organization that holds an installation, recording the installation of a known account.</summary>
  /// <param name="installation">The installation, as GitHub lists it.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The organization, or <see langword="null" /> when the account is not an organization of the instance.</returns>
  private async Task<InstallationOwner?> FindOwnerAsync(GitHubInstallationInfo installation, CancellationToken cancellationToken)
  {
    var owner = await installations.FindOwnerAsync(installation.Id, cancellationToken).ConfigureAwait(false);

    if (owner is not null)
    {
      return owner;
    }

    var organization = await directory.FindByAccountIdAsync(Forge.GitHub, installation.AccountId, cancellationToken).ConfigureAwait(false);

    if (organization is null)
    {
      return null;
    }

    try
    {
      await installations.AddAsync(Installation.Create(installation.Id, organization.Id, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }
    catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
    {
      // Recorded in the meantime, by an upload for instance: its row tells which organization holds it.
      return await installations.FindOwnerAsync(installation.Id, cancellationToken).ConfigureAwait(false);
    }

    return new InstallationOwner(organization.Id, organization.State);
  }

  #endregion

  #region Implementation of ICommandHandler<SyncInstallationsCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncInstallationsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (command is { TransferredRepositoryId: { Length: > 0 } repositoryId, PreviousOwnerAccountId: { Length: > 0 } previousOwner })
    {
      await QueueTransferAsync(repositoryId, previousOwner, cancellationToken).ConfigureAwait(false);

      return Unit.Value;
    }

    var listed = await gitHub.ListInstallationsAsync(cancellationToken).ConfigureAwait(false);

    // GitHub refuses every call made for a suspended installation: its repositories stay as they are.
    foreach (var installation in listed.Where(static i => !i.Suspended))
    {
      var owner = await FindOwnerAsync(installation, cancellationToken).ConfigureAwait(false);

      if (owner is not { State: OrganizationState.Active })
      {
        continue;
      }

      var key = RepositoryReconciliationJobs.InstallationKey(installation.Id);
      var job = JobRequest.ForOrganization(
        owner.OrganizationId,
        JobTypes.SyncInstallationRepositories,
        RepositoryReconciliationJobs.InstallationPayload(installation.Id),
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