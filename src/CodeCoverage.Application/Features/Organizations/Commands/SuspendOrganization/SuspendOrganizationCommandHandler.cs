// ------------------------------------------------------------------------------------------------
// <copyright file="SuspendOrganizationCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;

/// <summary>Handler for the <see cref="SuspendOrganizationCommand" /> command.</summary>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="clock">The clock.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class SuspendOrganizationCommandHandler(IOrganizationDirectory directory, IClock clock, IAuditLog auditLog)
  : ICommandHandler<SuspendOrganizationCommand, Unit>
{
  #region Implementation of ICommandHandler<SuspendOrganizationCommand,Unit>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">No organization of the forge holds the login.</exception>
  public async ValueTask<Unit> Handle(SuspendOrganizationCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var organization = await directory.FindByLoginAsync(command.Forge, command.Login, cancellationToken).ConfigureAwait(false)
                       ?? throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist.");

    if (organization.State is OrganizationState.Suspended)
    {
      return Unit.Value;
    }

    var now = clock.UtcNow;

    var suspended = await directory.ChangeAsync(
                        command.Forge,
                        organization.ExternalAccountId,
                        row => row.Suspend(SuspensionReason.Administrator, now),
                        cancellationToken
                      )
                      .ConfigureAwait(false);

    if (suspended is null)
    {
      throw new NotFoundException(OrganizationErrors.NotFound, "The organization no longer exists.");
    }

    await auditLog.RecordAsync("Organization.Suspend", $"{command.Forge}/{organization.Login}", null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
