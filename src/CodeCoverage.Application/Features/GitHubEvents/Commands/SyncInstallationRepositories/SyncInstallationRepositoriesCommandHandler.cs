// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationRepositoriesCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallationRepositories;

/// <summary>Handler for the <see cref="SyncInstallationRepositoriesCommand" /> command.</summary>
/// <param name="gitHub">The gateway of the App.</param>
/// <param name="installations">The repository of the installations.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="currentOrganization">The organization of the scope.</param>
/// <param name="reconciler">What the deliveries and the reconciliations share.</param>
/// <param name="clock">The clock.</param>
internal sealed class SyncInstallationRepositoriesCommandHandler(
  IGitHubAppGateway gitHub,
  IInstallationRepository installations,
  IRepositoryRepository repositories,
  ICurrentOrganization currentOrganization,
  RepositoryReconciler reconciler,
  IClock clock) : ICommandHandler<SyncInstallationRepositoriesCommand>
{
  #region Methods

  /// <summary>Tracks a repository the installation serves, or brings its row up to date.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <param name="info">What GitHub reports.</param>
  /// <param name="known">Whether the repository is already tracked.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the row is saved.</returns>
  private async Task UpsertAsync(long installationId, ForgeRepositoryInfo info, bool known, CancellationToken cancellationToken)
  {
    var repository = known ? await repositories.FindByExternalIdAsync(info.ExternalId, cancellationToken).ConfigureAwait(false) : null;

    var isNew = repository is null;
    var version = repository?.Version;

    if (repository is null)
    {
      repository = reconciler.Create(installationId, info);
    }
    else
    {
      repository.Activate(installationId, clock.UtcNow);
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

  #region Implementation of ICommandHandler<SyncInstallationRepositoriesCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncInstallationRepositoriesCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var owner = await installations.FindOwnerAsync(command.InstallationId, cancellationToken).ConfigureAwait(false);

    // The job names the installation in its payload: only the one of this organization is ever listed here.
    if (owner is null || owner.OrganizationId != currentOrganization.Id)
    {
      return Unit.Value;
    }

    var listed = await gitHub.ListInstallationRepositoriesAsync(command.InstallationId, cancellationToken).ConfigureAwait(false);


    var tracked = await repositories.ListAsync(cancellationToken).ConfigureAwait(false);
    var known = tracked.Select(static r => r.ExternalId).ToHashSet(StringComparer.Ordinal);
    var served = new HashSet<string>(StringComparer.Ordinal);

    foreach (var info in listed)
    {
      var externalId = TrackedRepository.NormalizeExternalId(info.ExternalId);

      served.Add(externalId);
      await UpsertAsync(command.InstallationId, info, known.Contains(externalId), cancellationToken).ConfigureAwait(false);
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