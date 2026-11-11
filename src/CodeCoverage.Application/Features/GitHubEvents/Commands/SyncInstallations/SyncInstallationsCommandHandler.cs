// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallations;

/// <summary>Handler for the <see cref="SyncInstallationsCommand" /> command.</summary>
/// <param name="gitHub">The gateway of the App.</param>
/// <param name="installations">The repository of the installations.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="lifecycle">What an installation does to the organization of its account.</param>
/// <remarks>
///   <para>
///     A coordinator (ADR-013): it reads and writes only global tables, <c>Installations</c> and
///     <c>Organizations</c>, and queues one job per active organization, by its identifier. An organization that
///     is not active gets no job, so none of its repositories is created.
///   </para>
///   <para>
///     It repairs the deliveries of the <c>installation</c> event that were lost, both ways: an unknown account
///     gets a pending organization, an organization whose installation is suspended or no longer listed is
///     suspended, and one that the removal of the application had suspended is reactivated when its installation
///     is listed again. Nothing here admits an organization.
///   </para>
/// </remarks>
internal sealed class SyncInstallationsCommandHandler(
  IGitHubAppGateway gitHub,
  IInstallationRepository installations,
  IOrganizationDirectory directory,
  IJobQueue jobQueue,
  InstallationLifecycle lifecycle) : ICommandHandler<SyncInstallationsCommand>
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

  /// <summary>Applies what the listing says of one installation to the organization of its account.</summary>
  /// <param name="installation">The installation, as GitHub lists it.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   The organization the installation serves, or <see langword="null" /> when it serves none: its account
  ///   cannot be an organization, or the installation is suspended.
  /// </returns>
  private async Task<Organization?> FollowAsync(GitHubInstallationInfo installation, CancellationToken cancellationToken)
  {
    var organization = await lifecycle.AttachAsync(installation, cancellationToken).ConfigureAwait(false);

    if (organization is null)
    {
      return null;
    }

    if (!installation.Suspended)
    {
      return await lifecycle.ReactivateAsync(organization, cancellationToken).ConfigureAwait(false);
    }

    // The delivery of the suspension was lost.
    if (organization.State is not OrganizationState.Suspended)
    {
      await lifecycle.SuspendAsync(organization.ExternalAccountId, cancellationToken).ConfigureAwait(false);
    }

    return null;
  }

  /// <summary>Suspends the active organizations whose installation GitHub no longer lists.</summary>
  /// <param name="served">The identifiers, on GitHub, of the accounts a listed installation serves.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the organizations are suspended.</returns>
  /// <remarks>
  ///   <para>
  ///     The delivery of the removal was lost. An organization that never had an installation is left alone: an
  ///     administrator may admit an account before it installs the application.
  ///   </para>
  ///   <para>
  ///     The pages of the listing are numbered: an installation added or removed between two readings shifts the
  ///     next page, and may leave one out. Each installation of the organization is therefore asked for on its own,
  ///     and the organization is suspended only when none of them is still active on GitHub.
  ///   </para>
  /// </remarks>
  private async Task SuspendUnservedAsync(HashSet<string> served, CancellationToken cancellationToken)
  {
    var active = await directory.ListActiveAsync(cancellationToken).ConfigureAwait(false);

    foreach (var organization in active.Where(o => o.Forge is Forge.GitHub && !served.Contains(o.ExternalAccountId)))
    {
      var recorded = await installations.ListIdsForOrganizationAsync(organization.Id, cancellationToken).ConfigureAwait(false);

      if (recorded.Count == 0 || await IsStillServedAsync(recorded, cancellationToken).ConfigureAwait(false))
      {
        continue;
      }

      await lifecycle.SuspendAsync(organization.ExternalAccountId, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Tells whether one of the installations of an organization is still active on GitHub.</summary>
  /// <param name="recorded">The identifiers of its installations.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns><see langword="true" /> when GitHub still knows one of them, not suspended.</returns>
  private async Task<bool> IsStillServedAsync(IReadOnlyList<long> recorded, CancellationToken cancellationToken)
  {
    foreach (var installationId in recorded)
    {
      if (await gitHub.GetInstallationAsync(installationId, cancellationToken).ConfigureAwait(false) is { Suspended: false })
      {
        return true;
      }
    }

    return false;
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

    // The listing is read to its last page or fails: a partial one would suspend the organizations it left out.
    var listed = await gitHub.ListInstallationsAsync(cancellationToken).ConfigureAwait(false);

    // By account, not by organization: an account that is listed is served even when it is left out below.
    var served = listed.Where(static i => !i.Suspended)
                       .Select(static i => Organization.NormalizeExternalAccountId(i.AccountId))
                       .ToHashSet(StringComparer.Ordinal);

    foreach (var installation in listed)
    {
      var organization = await FollowAsync(installation, cancellationToken).ConfigureAwait(false);

      if (organization is not { State: OrganizationState.Active })
      {
        continue;
      }

      var key = RepositoryReconciliationJobs.InstallationKey(installation.Id);
      var job = JobRequest.ForOrganization(
        organization.Id,
        JobTypes.SyncInstallationRepositories,
        RepositoryReconciliationJobs.InstallationPayload(installation.Id),
        key,
        $"sync:{key}",
        coalesce: true
      );

      await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
    }

    await SuspendUnservedAsync(served, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}