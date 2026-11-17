// ------------------------------------------------------------------------------------------------
// <copyright file="SyncConnectionRepositoriesCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Connections.Commands.SyncConnectionRepositories;

/// <summary>Handler for the <see cref="SyncConnectionRepositoriesCommand" /> command.</summary>
/// <param name="forges">The gateways of the forges.</param>
/// <param name="installations">The repository of the installations of GitHub.</param>
/// <param name="connections">The repository of the connections of the other forges.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="currentOrganization">The organization of the scope.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="reconciler">What the deliveries and the reconciliations share.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The account and the repositories are read through the neutral <see cref="IForgeDirectoryGateway" /> of the forge
///     of the connection. A repository listed is tracked or brought up to date, a tracked one no longer listed is
///     deactivated; a listing that fails changes nothing.
///   </para>
///   <para>
///     When the forge reports a new login for the account of the organization, the organization takes it here, and the
///     rewrite of the owner of its repositories is queued: the renaming step is the same for every forge.
///   </para>
/// </remarks>
internal sealed class SyncConnectionRepositoriesCommandHandler(
  IForgeGatewayProvider forges,
  IInstallationRepository installations,
  IForgeConnectionRepository connections,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ICurrentOrganization currentOrganization,
  IJobQueue jobQueue,
  RepositoryReconciler reconciler,
  IClock clock) : ICommandHandler<SyncConnectionRepositoriesCommand>
{
  #region Methods

  /// <summary>Tells whether the connection of a command serves the organization of the scope.</summary>
  /// <param name="command">The command.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   <see langword="true" /> when the connection is known, of that forge and of that organization, and not suspended;
  ///   otherwise <see langword="false" />.
  /// </returns>
  private async Task<bool> ServesTheScopeAsync(SyncConnectionRepositoriesCommand command, CancellationToken cancellationToken)
  {
    if (command.Forge is Forge.GitHub)
    {
      var installation = await installations.FindOwnerAsync(TrackedRepository.InstallationIdOf(command.ConnectionId), cancellationToken)
                                            .ConfigureAwait(false);

      return installation is not null && installation.OrganizationId == currentOrganization.Id;
    }

    var owner = await connections.FindOwnerAsync(command.ConnectionId, cancellationToken).ConfigureAwait(false);

    return owner is not null
           && owner.OrganizationId == currentOrganization.Id
           && owner.Forge == command.Forge
           && owner.Status is not ForgeConnectionStatus.Suspended;
  }

  /// <summary>Applies to the organization of the scope the login its forge now reports for its account.</summary>
  /// <param name="account">What the forge reports about the account, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the organization is renamed, or when there is nothing to rename.</returns>
  /// <remarks>
  ///   The organization is matched by the identifier of its account, never by a login, and a login that is not well
  ///   formed is ignored. The rewrite of the owner reads the login of the organization when it runs, so it also reaches
  ///   the repositories the forge no longer lists.
  /// </remarks>
  private async Task RenameAsync(ForgeAccountInfo? account, CancellationToken cancellationToken)
  {
    if (account is null || !Organization.IsWellFormedLogin(account.Login))
    {
      return;
    }

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

    if (organization is null
        || !string.Equals(organization.ExternalAccountId, Organization.NormalizeExternalAccountId(account.ExternalAccountId), StringComparison.Ordinal)
        || string.Equals(organization.Login, account.Login, StringComparison.Ordinal))
    {
      return;
    }

    var renamed = await directory.UpsertFromForgeAsync(organization.Forge, organization.ExternalAccountId, account.Login, organization.AccountType, cancellationToken)
                                 .ConfigureAwait(false);

    if (renamed.State is OrganizationState.Active)
    {
      await jobQueue.EnqueueAsync(RewriteRepositoryOwnerJobs.Create(), cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Tracks a repository the connection serves, or brings its row up to date.</summary>
  /// <param name="connection">The connection.</param>
  /// <param name="info">What the forge reports.</param>
  /// <param name="known">Whether the repository is already tracked.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the row is saved.</returns>
  private async Task UpsertAsync(ForgeConnectionRef connection, ForgeRepositoryInfo info, bool known, CancellationToken cancellationToken)
  {
    var repository = known ? await repositories.FindByExternalIdAsync(info.ExternalId, cancellationToken).ConfigureAwait(false) : null;

    var isNew = repository is null;
    var version = repository?.Version;

    if (repository is null)
    {
      repository = reconciler.Create(new ForgeConnectionInfo(connection.ConnectionId, info.OwnerExternalId ?? string.Empty, info.ExternalId, connection.Forge), info);
    }
    else
    {
      long? installationId = connection.Forge is Forge.GitHub ? TrackedRepository.InstallationIdOf(connection.ConnectionId) : null;

      repository.Activate(connection.ConnectionId, installationId, clock.UtcNow);
      reconciler.Apply(repository, info);
    }

    if (!isNew && repository.Version == version)
    {
      return;
    }

    try
    {
      await reconciler.SaveAsync(repository, isNew, job: null, cancellationToken).ConfigureAwait(false);
    }
    catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
    {
      // Tracked in the meantime by an upload, or its name is still held by a tracked row that the same list
      // renames further on: the others go on, and the next reconciliation tries this one again.
    }
  }

  #endregion

  #region Implementation of ICommandHandler<SyncConnectionRepositoriesCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncConnectionRepositoriesCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    // The job names the connection in its payload: only the one of this organization is ever listed here.
    if (!await ServesTheScopeAsync(command, cancellationToken).ConfigureAwait(false))
    {
      return Unit.Value;
    }

    var connection = new ForgeConnectionRef(command.Forge, command.ConnectionId);
    var gateway = forges.GetDirectory(command.Forge);

    await RenameAsync(await gateway.GetAccountAsync(connection, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    var listed = await gateway.ListRepositoriesAsync(connection, cancellationToken).ConfigureAwait(false);

    var tracked = await repositories.ListAsync(cancellationToken).ConfigureAwait(false);
    var known = tracked.Select(static r => r.ExternalId).ToHashSet(StringComparer.Ordinal);
    var served = new HashSet<string>(StringComparer.Ordinal);

    foreach (var info in listed)
    {
      var externalId = TrackedRepository.NormalizeExternalId(info.ExternalId);

      served.Add(externalId);
      await UpsertAsync(connection, info, known.Contains(externalId), cancellationToken).ConfigureAwait(false);
    }

    foreach (var missing in tracked.Where(r => r.IsActive && !served.Contains(r.ExternalId)))
    {
      var repository = await repositories.FindByExternalIdAsync(missing.ExternalId, cancellationToken).ConfigureAwait(false);

      if (repository is { IsActive: true })
      {
        repository.Deactivate(clock.UtcNow);
        await repositories.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
      }
    }

    return Unit.Value;
  }

  #endregion
}
