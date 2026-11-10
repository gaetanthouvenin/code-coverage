// ------------------------------------------------------------------------------------------------
// <copyright file="HandleWebhookDeliveryCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.HandleWebhookDelivery;

/// <summary>Handler for the <see cref="HandleWebhookDeliveryCommand" /> command.</summary>
/// <param name="deliveries">The repository of the deliveries.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="currentOrganization">The organization of the scope.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="reconciler">What the deliveries and the reconciliations share.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   Every branch gives the same result when it runs twice: a delivery GitHub sends again, or a job that is
///   retried, changes nothing more. An event this handler does not know yet is recorded as handled.
/// </remarks>
internal sealed class HandleWebhookDeliveryCommandHandler(
  IWebhookDeliveryRepository deliveries,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ICurrentOrganization currentOrganization,
  IJobQueue jobQueue,
  RepositoryReconciler reconciler,
  IClock clock) : ICommandHandler<HandleWebhookDeliveryCommand>
{
  #region Fields

  private const string InstallationEvent = "installation";

  private const string InstallationRepositoriesEvent = "installation_repositories";

  private const string RepositoryEvent = "repository";

  #endregion

  #region Methods

  /// <summary>Handles an event about an installation or about the repositories it serves.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the event is handled.</returns>
  private async Task HandleInstallationAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    // The account is not an active organization: only the coordinator may look at it, and it creates no repository.
    if (!currentOrganization.IsKnown)
    {
      await jobQueue.EnqueueAsync(SyncInstallationsJobs.Create(), cancellationToken).ConfigureAwait(false);

      return;
    }

    if (delivery.Event is InstallationRepositoriesEvent)
    {
      foreach (var externalId in GitHubRepositoryEvent.Parse(delivery.Payload).RemovedRepositoryIds)
      {
        await DeactivateAsync(
          await repositories.FindByExternalIdAsync(externalId, cancellationToken).ConfigureAwait(false),
          cancellationToken
        ).ConfigureAwait(false);
      }
    }

    // GitHub answers nothing for an installation that was deleted or suspended: there is nothing to list.
    if (delivery.InstallationId is { } installationId && delivery.Action is not ("deleted" or "suspend"))
    {
      await jobQueue.EnqueueAsync(RepositoryReconciliationJobs.ForInstallation(installationId), cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Handles an event about one repository, inside its organization.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the event is handled.</returns>
  private async Task HandleRepositoryAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    var body = GitHubRepositoryEvent.Parse(delivery.Payload);

    if (body.Repository is not { } info)
    {
      return;
    }

    var repository = await repositories.FindByExternalIdAsync(info.ExternalId, cancellationToken).ConfigureAwait(false);

    switch (delivery.Action)
    {
      case "renamed" or "privatized" or "publicized":
        if (repository is not null && reconciler.Apply(repository, info))
        {
          await reconciler.SaveAsync(repository, isNew: false, job: null, cancellationToken).ConfigureAwait(false);
        }

        break;
      case "deleted":
        await DeactivateAsync(repository, cancellationToken).ConfigureAwait(false);

        break;
      case "transferred":
        await HandleTransferAsync(delivery, body, info, repository, cancellationToken).ConfigureAwait(false);

        break;
    }
  }

  /// <summary>Handles the transfer of a repository, on the side of the account that receives it or of the one it left.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="body">What the body says.</param>
  /// <param name="info">The repository, as the body describes it.</param>
  /// <param name="repository">Its row in the organization of the scope, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the transfer is handled.</returns>
  /// <remarks>
  ///   No data moves (ADR-013). The recipient gets a row of its own, or its former row again without what
  ///   was revoked when the repository left, and queues the reconciliation that closes the row of the origin.
  /// </remarks>
  private async Task HandleTransferAsync(
    GitHubWebhookEvent delivery,
    GitHubRepositoryEvent body,
    ForgeRepositoryInfo info,
    TrackedRepository? repository,
    CancellationToken cancellationToken)
  {
    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("The scope has not entered an organization.");

    if (!string.Equals(body.OwnerAccountId, organization.ExternalAccountId, StringComparison.OrdinalIgnoreCase))
    {
      // The repository belongs to another account now: this organization is the one it left.
      if (repository is not null)
      {
        await reconciler.CloseTransferredAsync(repository, cancellationToken).ConfigureAwait(false);
      }

      return;
    }

    if (delivery.InstallationId is not { } installationId || info.DefaultBranch.Length == 0)
    {
      return;
    }

    var isNew = repository is null;

    if (repository is null)
    {
      repository = reconciler.Create(installationId, info);
    }
    else
    {
      repository.Activate(installationId, clock.UtcNow);
      reconciler.Apply(repository, info);
    }

    var reconciliation = body.PreviousOwnerAccountId is { } previousOwner
                           ? SyncInstallationsJobs.CreateForTransfer(info.ExternalId, previousOwner)
                           : SyncInstallationsJobs.Create();

    await reconciler.SaveAsync(repository, isNew, reconciliation, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Stops tracking a repository that is still tracked.</summary>
  /// <param name="repository">The repository, read in this scope, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved.</returns>
  private async Task DeactivateAsync(TrackedRepository? repository, CancellationToken cancellationToken)
  {
    if (repository is { IsActive: true })
    {
      repository.Deactivate(clock.UtcNow);
      await repositories.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
    }
  }

  #endregion

  #region Implementation of ICommandHandler<HandleWebhookDeliveryCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The delivery is not stored, or belongs to another organization.</exception>
  public async ValueTask<Unit> Handle(HandleWebhookDeliveryCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var delivery = await deliveries.FindForProcessingAsync(command.DeliveryId, cancellationToken).ConfigureAwait(false)
                   ?? throw new NotFoundException(GitHubEventErrors.DeliveryNotFound, "The delivery to handle does not exist.");

    switch (delivery.Event)
    {
      case InstallationEvent or InstallationRepositoriesEvent:
        await HandleInstallationAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
      case RepositoryEvent when currentOrganization.IsKnown:
        await HandleRepositoryAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
    }

    await deliveries.MarkProcessedAsync(delivery.DeliveryId, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}