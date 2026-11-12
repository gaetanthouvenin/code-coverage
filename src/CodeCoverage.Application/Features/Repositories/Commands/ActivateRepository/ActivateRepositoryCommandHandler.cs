// ------------------------------------------------------------------------------------------------
// <copyright file="ActivateRepositoryCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Commands.ActivateRepository;

/// <summary>Handler for the <see cref="ActivateRepositoryCommand" /> command.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="auditLog">The audit log.</param>
/// <param name="clock">The clock.</param>
internal sealed class ActivateRepositoryCommandHandler(
  CommitLocator locator,
  IRepositoryRepository repositories,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<ActivateRepositoryCommand>
{
  #region Fields

  /// <summary>The audit action of an enabled repository.</summary>
  internal const string AuditAction = "Repositories.Activate";

  #endregion

  #region Implementation of ICommandHandler<ActivateRepositoryCommand,Unit>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(ActivateRepositoryCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = await locator.FindRepositoryAsync(command.Owner, command.Name, cancellationToken).ConfigureAwait(false);

    if (!repository.Enable(clock.UtcNow))
    {
      return Unit.Value;
    }

    await repositories.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync(AuditAction, repository.Id.ToString(CultureInfo.InvariantCulture), null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
