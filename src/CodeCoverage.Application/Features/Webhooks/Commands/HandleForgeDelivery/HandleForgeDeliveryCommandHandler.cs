// ------------------------------------------------------------------------------------------------
// <copyright file="HandleForgeDeliveryCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Webhooks.Commands.HandleForgeDelivery;

/// <summary>Handler for the <see cref="HandleForgeDeliveryCommand" /> command.</summary>
/// <param name="deliveries">The repository of the deliveries.</param>
/// <param name="forges">The adapters of the forges, which give the translator of the forge of the delivery.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="currentOrganization">The organization of the scope.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="pushes">What applies a push to its branch.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The translator of the forge reads the body; this handler sees only the neutral event it gives, so a new forge
///     adds a translator, never a use case:
///   </para>
///   <list type="bullet">
///     <item><see cref="ForgePushEvent" /> moves the branch of a tracked repository;</item>
///     <item><see cref="ForgePullRequestEvent" /> queues the synchronization of the pull request;</item>
///     <item>
///       <see cref="ForgeRepositoryEvent" /> queues the reconciliation of the connection, which tracks, renames or
///       deactivates the repository and applies a renaming of the account; a repository transferred to another account
///       queues the closing of its row instead.
///     </item>
///   </list>
///   <para>
///     An event the translator ignores, or that this handler does not know, is recorded as handled, never as a failure.
///     Every branch gives the same result when it runs twice: a delivery the forge sends again, or a job that is
///     retried, changes nothing more.
///   </para>
/// </remarks>
internal sealed class HandleForgeDeliveryCommandHandler(
  IWebhookDeliveryRepository deliveries,
  IForgeGatewayProvider forges,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ICurrentOrganization currentOrganization,
  IJobQueue jobQueue,
  PushProcessor pushes,
  IClock clock) : ICommandHandler<HandleForgeDeliveryCommand>
{
  #region Methods

  /// <summary>Applies a push to a tracked repository of the organization.</summary>
  /// <param name="organization">The organization of the scope.</param>
  /// <param name="push">The push.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the push is applied, or when the repository is not tracked.</returns>
  private async Task HandlePushAsync(Organization organization, ForgePushEvent push, CancellationToken cancellationToken)
  {
    var repository = await repositories.FindByExternalIdAsync(push.RepositoryExternalId, cancellationToken).ConfigureAwait(false);

    if (repository is { IsActive: true })
    {
      await pushes.ApplyAsync(repository, repository.ToForgeRef(organization.Forge), push, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Queues the synchronization of a pull request of a tracked repository.</summary>
  /// <param name="change">The change of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the synchronization is queued, or when the repository is not tracked.</returns>
  /// <remarks>Only the number is used: the job reads the pull request from the forge.</remarks>
  private async Task HandlePullRequestAsync(ForgePullRequestEvent change, CancellationToken cancellationToken)
  {
    var repository = await repositories.FindByExternalIdAsync(change.RepositoryExternalId, cancellationToken).ConfigureAwait(false);

    if (repository is { IsActive: true })
    {
      await jobQueue.EnqueueAsync(SyncPullRequestJobs.Create(repository.Id, change.Number), cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Queues what a change of a repository asks for.</summary>
  /// <param name="delivery">The delivery, which names its connection.</param>
  /// <param name="organization">The organization of the scope.</param>
  /// <param name="change">The change of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the job is queued.</returns>
  /// <remarks>
  ///   Nothing is decided from the body but the account that holds a transferred repository: the reconciliation reads
  ///   the forge. A transfer whose new account the forge does not name is reconciled like any other change.
  /// </remarks>
  private async Task HandleRepositoryAsync(
    ForgeDelivery delivery,
    Organization organization,
    ForgeRepositoryEvent change,
    CancellationToken cancellationToken)
  {
    if (change.Kind is ForgeRepositoryEventKind.Transferred
        && change.Repository.OwnerExternalId is { } owner
        && !string.Equals(Organization.NormalizeExternalAccountId(owner), organization.ExternalAccountId, StringComparison.Ordinal))
    {
      // The repository belongs to another account now: this organization is the one it left.
      var externalId = change.Repository.ExternalId;
      var closing = JobRequest.Create(
        JobTypes.ReconcileRepositoryTransfer,
        RepositoryReconciliationJobs.TransferPayload(delivery.Forge, externalId),
        RepositoryReconciliationJobs.RepositoryKey(externalId),
        $"transfer:{externalId}",
        coalesce: true
      );

      await jobQueue.EnqueueAsync(closing, cancellationToken).ConfigureAwait(false);

      return;
    }

    await jobQueue.EnqueueAsync(RepositoryReconciliationJobs.ForConnection(delivery.Forge, delivery.ConnectionId), cancellationToken)
                  .ConfigureAwait(false);
  }

  #endregion

  #region Implementation of ICommandHandler<HandleForgeDeliveryCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The delivery is not stored, names no connection, or belongs to another organization.</exception>
  /// <exception cref="NotSupportedException">No event translator is implemented for the forge of the delivery.</exception>
  public async ValueTask<Unit> Handle(HandleForgeDeliveryCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var delivery = await deliveries.FindForgeDeliveryForProcessingAsync(command.Forge, command.DeliveryId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfForgeDeliveryNotFound(delivery);

    // A delivery of a connection always runs in its organization: out of one, or of another forge, there is nothing to apply.
    var organization = currentOrganization.IsKnown ? await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) : null;

    if (organization is not null && organization.Forge == delivery.Forge)
    {
      switch (forges.GetEventTranslator(delivery.Forge).Translate(delivery))
      {
        case ForgePushEvent push:
          await HandlePushAsync(organization, push, cancellationToken).ConfigureAwait(false);

          break;
        case ForgePullRequestEvent change:
          await HandlePullRequestAsync(change, cancellationToken).ConfigureAwait(false);

          break;
        case ForgeRepositoryEvent change:
          await HandleRepositoryAsync(delivery, organization, change, cancellationToken).ConfigureAwait(false);

          break;
      }
    }

    await deliveries.MarkForgeDeliveryProcessedAsync(delivery.Forge, delivery.DeliveryId, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
