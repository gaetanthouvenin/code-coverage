// ------------------------------------------------------------------------------------------------
// <copyright file="ReconcileRepositoryTransferCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Commands.ReconcileRepositoryTransfer;

/// <summary>Handler for the <see cref="ReconcileRepositoryTransferCommand" /> command.</summary>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="reconciler">What the deliveries and the reconciliations share.</param>
/// <remarks>
///   An organization serves one forge: an identifier of another forge names none of its repositories, even when the
///   text is the same, so the job then changes nothing.
/// </remarks>
internal sealed class ReconcileRepositoryTransferCommandHandler(
  IOrganizationDirectory directory,
  IRepositoryRepository repositories,
  RepositoryReconciler reconciler) : ICommandHandler<ReconcileRepositoryTransferCommand>
{
  #region Implementation of ICommandHandler<ReconcileRepositoryTransferCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(ReconcileRepositoryTransferCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

    if (organization is null || organization.Forge != command.Forge)
    {
      return Unit.Value;
    }

    var repository = await repositories.FindByExternalIdAsync(command.RepositoryExternalId, cancellationToken).ConfigureAwait(false);

    // Never tracked here: the organization holds nothing of this repository.
    if (repository is not null)
    {
      await reconciler.CloseTransferredAsync(repository, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
