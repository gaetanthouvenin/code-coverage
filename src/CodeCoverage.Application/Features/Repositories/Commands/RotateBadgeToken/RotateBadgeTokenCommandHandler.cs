// ------------------------------------------------------------------------------------------------
// <copyright file="RotateBadgeTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Commands.RotateBadgeToken;

/// <summary>Handler for the <see cref="RotateBadgeTokenCommand" /> command.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="generator">Generates the badge token.</param>
/// <param name="protector">Protects the badge token before it is stored.</param>
/// <param name="auditLog">The audit trail.</param>
/// <param name="clock">The clock.</param>
internal sealed class RotateBadgeTokenCommandHandler(
  CommitLocator locator,
  IRepositoryRepository repositories,
  ITokenGenerator generator,
  ISecretProtector protector,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<RotateBadgeTokenCommand, string>
{
  #region Fields

  /// <summary>The audit action of a replaced badge token.</summary>
  internal const string AuditAction = "Repositories.RotateBadgeToken";

  #endregion

  #region Implementation of ICommandHandler<RotateBadgeTokenCommand,string>

  /// <inheritdoc />
  public async ValueTask<string> Handle(RotateBadgeTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = await locator.FindRepositoryAsync(command.Owner, command.Name, cancellationToken).ConfigureAwait(false);
    var value = generator.NewBadgeToken();

    repository.RotateBadgeToken(protector.Protect(value), clock.UtcNow);
    await repositories.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync(AuditAction, repository.Id.ToString(CultureInfo.InvariantCulture), null, cancellationToken).ConfigureAwait(false);

    return value;
  }

  #endregion
}
