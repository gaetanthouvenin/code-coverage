// ------------------------------------------------------------------------------------------------
// <copyright file="HandleWebhookDeliveryCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.HandleWebhookDelivery;

/// <summary>Handler for the <see cref="HandleWebhookDeliveryCommand" /> command.</summary>
/// <param name="deliveries">The repository of the deliveries.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="installations">The repository of the installations.</param>
/// <param name="currentOrganization">The organization of the scope.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="reconciler">What the deliveries and the reconciliations share.</param>
/// <param name="lifecycle">What an installation does to the organization of its account.</param>
/// <param name="pushes">What applies a push to its branch.</param>
/// <param name="users">The repository of the users.</param>
/// <param name="auditLog">The audit trail.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   Every branch gives the same result when it runs twice: a delivery GitHub sends again, or a job that is
///   retried, changes nothing more. An event this handler does not know yet is recorded as handled.
/// </remarks>
internal sealed class HandleWebhookDeliveryCommandHandler(
  IWebhookDeliveryRepository deliveries,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  IInstallationRepository installations,
  ICurrentOrganization currentOrganization,
  IJobQueue jobQueue,
  RepositoryReconciler reconciler,
  InstallationLifecycle lifecycle,
  PushProcessor pushes,
  IUserRepository users,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<HandleWebhookDeliveryCommand>
{
  #region Fields

  private const string InstallationEvent = "installation";

  private const string InstallationRepositoriesEvent = "installation_repositories";

  private const string RepositoryEvent = "repository";

  private const string PushEvent = "push";

  private const string PullRequestEvent = "pull_request";

  private const string InstallationTargetEvent = "installation_target";

  private const string OrganizationEvent = "organization";

  private const string MemberEvent = "member";

  private const string MembershipEvent = "membership";

  #endregion

  #region Methods

  /// <summary>Handles an event about an installation or about the repositories it serves.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the event is handled.</returns>
  private async Task HandleInstallationAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    var suspended = delivery.Event is InstallationEvent && await FollowLifecycleAsync(delivery, cancellationToken).ConfigureAwait(false);

    // The account is not an active organization: only the coordinator may look at it, and it creates no repository.
    // After a suspension it also tells whether another installation still serves the account, when the deliveries
    // of a reinstallation arrived out of order.
    if (!currentOrganization.IsKnown || suspended)
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

  /// <summary>Applies what the creation, the removal or the suspension of an installation does to its organization.</summary>
  /// <param name="delivery">The delivery of an <c>installation</c> event.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns><see langword="true" /> when the delivery reports an installation that no longer serves its account.</returns>
  /// <remarks>
  ///   <c>created</c> and <c>unsuspend</c> record the installation under the organization of its account, pending
  ///   when the account is new, and lift a suspension the removal of the application caused. <c>deleted</c> and
  ///   <c>suspend</c> suspend the organization, unless they name an installation older than the most recent one it
  ///   has: a delivery that arrived after the account installed the application again. No action admits one.
  /// </remarks>
  private async Task<bool> FollowLifecycleAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    switch (delivery.Action)
    {
      case "created" or "unsuspend":
        if (GitHubInstallationEvent.Parse(delivery.Payload).Installation is { } installation
            && await lifecycle.AttachAsync(installation, cancellationToken).ConfigureAwait(false) is { } organization)
        {
          await lifecycle.ReactivateAsync(organization, cancellationToken).ConfigureAwait(false);
        }

        return false;
      case "deleted" or "suspend":
        // Inside an organization, the account is the one of the scope, which the job took from its own row.
        var target = currentOrganization.IsKnown
                       ? await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false)
                       : GitHubInstallationEvent.Parse(delivery.Payload).Installation?.AccountId is { } bodyAccountId
                         ? await directory.FindByAccountIdAsync(Forge.GitHub, bodyAccountId, cancellationToken).ConfigureAwait(false)
                         : null;

        if (target is null)
        {
          return true;
        }

        // The account installed the application again meanwhile: the removal of a former installation suspends nothing.
        if (delivery.InstallationId is { } removed
            && await installations.ListIdsForOrganizationAsync(target.Id, cancellationToken).ConfigureAwait(false) is [var current, ..]
            && current != removed)
        {
          return false;
        }

        await lifecycle.SuspendAsync(target.ExternalAccountId, cancellationToken).ConfigureAwait(false);

        return true;
      default:
        return false;
    }
  }

  /// <summary>Applies the rename of the account of an installation, an organization or a personal account.</summary>
  /// <param name="delivery">The delivery of an <c>installation_target</c> event.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the rename is applied.</returns>
  /// <remarks>
  ///   <para>
  ///     The organization is found by the identifier of the account, never by a login. An account this instance
  ///     does not know is left alone: only an installation creates an organization. A login that is not well
  ///     formed is ignored, and the delivery is recorded as handled.
  ///   </para>
  ///   <para>
  ///     A reserved login suspends an active organization, with an audit entry; it stays suspended until an
  ///     administrator acts. Otherwise, in the scope of that organization, the rewrite of the owner of its
  ///     repositories is queued, even when the login was already applied: the delivery may be handled again
  ///     after a failure between the two.
  ///   </para>
  /// </remarks>
  private async Task HandleAccountRenamedAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    var body = GitHubInstallationTargetEvent.Parse(delivery.Payload);

    if (body.AccountId is not { } accountId
        || body.Login is not { } login
        || !Organization.IsWellFormedLogin(login)
        || await directory.FindByAccountIdAsync(Forge.GitHub, accountId, cancellationToken).ConfigureAwait(false) is not { } known)
    {
      return;
    }

    var wasActive = known.State is OrganizationState.Active;
    var renamed = await directory.UpsertFromForgeAsync(Forge.GitHub, accountId, login, body.AccountType ?? known.AccountType, cancellationToken)
                                 .ConfigureAwait(false);

    if (renamed.State is not OrganizationState.Active)
    {
      if (wasActive && renamed.SuspensionReason is SuspensionReason.ReservedLogin)
      {
        await auditLog.RecordAsync(
                        "Organization.Suspend",
                        $"{Forge.GitHub}/{renamed.Login}",
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["Reason"] = nameof(SuspensionReason.ReservedLogin) },
                        cancellationToken
                      )
                      .ConfigureAwait(false);
      }

      return;
    }

    // The job runs in the organization of the scope: it is queued only when that organization is the renamed one.
    if (currentOrganization.IsKnown
        && await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) is { } current
        && string.Equals(current.ExternalAccountId, renamed.ExternalAccountId, StringComparison.Ordinal))
    {
      await jobQueue.EnqueueAsync(RewriteRepositoryOwnerJobs.Create(), cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Queues the refresh of the memberships of the user an event is about.</summary>
  /// <param name="delivery">The delivery of an <c>organization</c>, <c>member</c> or <c>membership</c> event.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the refresh is queued, or when there is none to queue.</returns>
  /// <remarks>
  ///   Nothing is decided from the body: the job asks GitHub. A user who never signed in has no membership to
  ///   refresh, and an action that names no user queues nothing.
  /// </remarks>
  private async Task HandleMemberAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    if (GitHubMemberEvent.Parse(delivery.Payload).UserExternalId is { } externalId
        && await users.FindByExternalIdAsync(Forge.GitHub, externalId, cancellationToken).ConfigureAwait(false) is { } user)
    {
      await jobQueue.EnqueueAsync(RefreshUserMembershipsJobs.Create(user.Id), cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Translates a <c>push</c> delivery into the neutral push of a branch.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <returns>The push, or <see langword="null" /> when the delivery names no repository or is not about a branch, a tag for instance.</returns>
  private static ForgePushEvent? ToPushEvent(GitHubWebhookEvent delivery)
  {
    if (delivery.RepositoryId is not { } repositoryId || GitHubPushEvent.Parse(delivery.Payload) is not { Branch: { } branch } push)
    {
      return null;
    }

    return new ForgePushEvent(repositoryId.ToString(CultureInfo.InvariantCulture), branch, push.Head, push.Deleted, push.TouchesConfig, push.Before);
  }

  /// <summary>Translates a <c>pull_request</c> delivery into the neutral change of a pull request.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <returns>
  ///   The change, or <see langword="null" /> when the delivery names no repository or no pull request, or reports an
  ///   action that changes nothing that is recorded, a label or a review request for instance.
  /// </returns>
  private static ForgePullRequestEvent? ToPullRequestEvent(GitHubWebhookEvent delivery)
  {
    if (delivery.Action is not ("opened" or "synchronize" or "reopened" or "edited" or "ready_for_review" or "closed")
        || delivery.RepositoryId is not { } repositoryId
        || GitHubPullRequestEvent.Parse(delivery.Payload).Number is not { } number)
    {
      return null;
    }

    return new ForgePullRequestEvent(repositoryId.ToString(CultureInfo.InvariantCulture), number);
  }

  /// <summary>Translates a <c>repository</c> delivery into the neutral change of a repository.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <returns>
  ///   The change, or <see langword="null" /> when the body names no repository that can be read, or the action is
  ///   one that changes nothing that is recorded, an archiving for instance.
  /// </returns>
  /// <remarks><c>privatized</c> and <c>publicized</c> change the repository in place, like a renaming.</remarks>
  private static ForgeRepositoryEvent? ToRepositoryEvent(GitHubWebhookEvent delivery)
  {
    ForgeRepositoryEventKind? kind = delivery.Action switch
    {
      "created" => ForgeRepositoryEventKind.Created,
      "renamed" or "privatized" or "publicized" => ForgeRepositoryEventKind.Renamed,
      "deleted" => ForgeRepositoryEventKind.Deleted,
      "transferred" => ForgeRepositoryEventKind.Transferred,
      _ => null,
    };

    if (kind is not { } known || GitHubRepositoryEvent.Parse(delivery.Payload) is not { Repository: { } repository } body)
    {
      return null;
    }

    return new ForgeRepositoryEvent(known, repository, body.PreviousOwnerAccountId);
  }

  /// <summary>Handles a push to a repository that is tracked, inside its organization.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the push is applied.</returns>
  private async Task HandlePushAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    if (ToPushEvent(delivery) is not { } push)
    {
      return;
    }

    var repository = await repositories.FindByExternalIdAsync(push.RepositoryExternalId, cancellationToken).ConfigureAwait(false);

    if (repository is not { IsActive: true })
    {
      return;
    }

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("The scope has not entered an organization.");

    await pushes.ApplyAsync(repository, repository.ToForgeRef(organization.Forge), push, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Handles an event about a pull request of a repository that is tracked, inside its organization.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the synchronization of the pull request is queued, or when there is none to queue.</returns>
  /// <remarks>
  ///   Only the number is read from the body: the job reads the pull request from the forge. The other actions, a
  ///   label or a review request for instance, change nothing that is recorded and cost no call to the forge.
  /// </remarks>
  private async Task HandlePullRequestAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    if (ToPullRequestEvent(delivery) is not { } change)
    {
      return;
    }

    var repository = await repositories.FindByExternalIdAsync(change.RepositoryExternalId, cancellationToken).ConfigureAwait(false);

    if (repository is { IsActive: true })
    {
      await jobQueue.EnqueueAsync(SyncPullRequestJobs.Create(repository.Id, change.Number), cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Handles an event about one repository, inside its organization.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the event is handled.</returns>
  /// <remarks>
  ///   A created repository changes nothing here: the <c>installation_repositories</c> delivery that gives it to the
  ///   installation queues the listing that tracks it.
  /// </remarks>
  private async Task HandleRepositoryAsync(GitHubWebhookEvent delivery, CancellationToken cancellationToken)
  {
    if (ToRepositoryEvent(delivery) is not { } change || change.Kind is ForgeRepositoryEventKind.Created)
    {
      return;
    }

    var info = change.Repository;
    var repository = await repositories.FindByExternalIdAsync(info.ExternalId, cancellationToken).ConfigureAwait(false);

    if (change.Kind is ForgeRepositoryEventKind.Renamed)
    {
      if (repository is not null && reconciler.Apply(repository, info))
      {
        await reconciler.SaveAsync(repository, isNew: false, job: null, cancellationToken).ConfigureAwait(false);
      }
    }
    else if (change.Kind is ForgeRepositoryEventKind.Deleted)
    {
      await DeactivateAsync(repository, cancellationToken).ConfigureAwait(false);
    }
    else
    {
      await HandleTransferAsync(delivery.InstallationId, change, repository, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Handles the transfer of a repository, on the side of the account that receives it or of the one it left.</summary>
  /// <param name="installationId">The installation of the delivery, or <see langword="null" />.</param>
  /// <param name="change">The transfer.</param>
  /// <param name="repository">Its row in the organization of the scope, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the transfer is handled.</returns>
  /// <remarks>
  ///   No data moves (ADR-013). The recipient gets a row of its own, or its former row again without what
  ///   was revoked when the repository left, and queues the reconciliation that closes the row of the origin.
  /// </remarks>
  private async Task HandleTransferAsync(
    long? installationId,
    ForgeRepositoryEvent change,
    TrackedRepository? repository,
    CancellationToken cancellationToken)
  {
    var info = change.Repository;
    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("The scope has not entered an organization.");

    if (!string.Equals(info.OwnerExternalId, organization.ExternalAccountId, StringComparison.OrdinalIgnoreCase))
    {
      // The repository belongs to another account now: this organization is the one it left.
      if (repository is not null)
      {
        await reconciler.CloseTransferredAsync(repository, cancellationToken).ConfigureAwait(false);
      }

      return;
    }

    if (installationId is not { } installation || info.DefaultBranch.Length == 0)
    {
      return;
    }

    var isNew = repository is null;

    if (repository is null)
    {
      repository = reconciler.Create(installation, info);
    }
    else
    {
      repository.Activate(installation, clock.UtcNow);
      reconciler.Apply(repository, info);
    }

    var reconciliation = change.PreviousOwnerExternalId is { } previousOwner
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

    var delivery = await deliveries.FindForProcessingAsync(command.DeliveryId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfWebhookDeliveryNotFound(delivery);

    switch (delivery.Event)
    {
      case InstallationEvent or InstallationRepositoriesEvent:
        await HandleInstallationAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
      case RepositoryEvent when currentOrganization.IsKnown:
        await HandleRepositoryAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
      case PushEvent when currentOrganization.IsKnown:
        await HandlePushAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
      case PullRequestEvent when currentOrganization.IsKnown:
        await HandlePullRequestAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
      case InstallationTargetEvent when delivery.Action is "renamed":
        await HandleAccountRenamedAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
      case OrganizationEvent or MemberEvent or MembershipEvent when currentOrganization.IsKnown:
        await HandleMemberAsync(delivery, cancellationToken).ConfigureAwait(false);

        break;
    }

    await deliveries.MarkProcessedAsync(delivery.DeliveryId, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
