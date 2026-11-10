// ------------------------------------------------------------------------------------------------
// <copyright file="ReconcileRepositoryTransferCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.ReconcileRepositoryTransfer;

/// <summary>Handler for the <see cref="ReconcileRepositoryTransferCommand" /> command.</summary>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="reconciler">What the deliveries and the reconciliations share.</param>
internal sealed class ReconcileRepositoryTransferCommandHandler(IRepositoryRepository repositories, RepositoryReconciler reconciler)
  : ICommandHandler<ReconcileRepositoryTransferCommand>
{
  #region Implementation of ICommandHandler<ReconcileRepositoryTransferCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(ReconcileRepositoryTransferCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

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