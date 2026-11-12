// ------------------------------------------------------------------------------------------------
// <copyright file="ImportBadgeTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Commands.ImportBadgeToken;

/// <summary>Handler for the <see cref="ImportBadgeTokenCommand" /> command.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="protector">Protects the badge token before it is stored.</param>
/// <param name="auditLog">The audit trail.</param>
/// <param name="clock">The clock.</param>
internal sealed class ImportBadgeTokenCommandHandler(
  CommitLocator locator,
  IRepositoryRepository repositories,
  ISecretProtector protector,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<ImportBadgeTokenCommand>
{
  #region Fields

  /// <summary>The audit action of an imported badge token.</summary>
  internal const string AuditAction = "Repositories.ImportBadgeToken";

  #endregion

  #region Implementation of ICommandHandler<ImportBadgeTokenCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(ImportBadgeTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = await locator.FindRepositoryAsync(command.Owner, command.Name, cancellationToken).ConfigureAwait(false);

    repository.RotateBadgeToken(protector.Protect(command.Value), clock.UtcNow);
    await repositories.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync(AuditAction, repository.Id.ToString(CultureInfo.InvariantCulture), null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
