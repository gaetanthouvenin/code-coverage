// ------------------------------------------------------------------------------------------------
// <copyright file="StartCodecovImportCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Codecov;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Commands.StartCodecovImport;

/// <summary>Handler for the <see cref="StartCodecovImportCommand" /> command.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="imports">The imports of the history of Codecov.</param>
/// <param name="protector">Protects the token before it is stored.</param>
/// <param name="auditLog">The audit trail.</param>
/// <param name="clock">The clock.</param>
internal sealed class StartCodecovImportCommandHandler(
  CommitLocator locator,
  ICodecovImportRepository imports,
  ISecretProtector protector,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<StartCodecovImportCommand>
{
  #region Fields

  /// <summary>The audit action of a started import.</summary>
  internal const string AuditAction = "Repositories.StartCodecovImport";

  #endregion

  #region Implementation of ICommandHandler<StartCodecovImportCommand>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">The forge has no counterpart on Codecov.</exception>
  public async ValueTask<Unit> Handle(StartCodecovImportCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    // Azure DevOps has no Codecov: refused here, rather than a job that would fail until it dies.
    UnprocessableEntityException.ThrowIfCodecovUnsupportedForge(command.Forge);

    var repository = await locator.FindRepositoryAsync(command.Owner, command.Name, cancellationToken).ConfigureAwait(false);

    // The row and its job in one transaction: the payload names the row, never the token.
    await imports.StartAsync(CodecovImport.Start(repository.Id, protector.Protect(command.Token), clock.UtcNow), cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync(AuditAction, repository.Id.ToString(CultureInfo.InvariantCulture), null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
