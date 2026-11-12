// ------------------------------------------------------------------------------------------------
// <copyright file="ResetOrganizationSettingCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Organizations.Commands.ResetOrganizationSetting;

/// <summary>Handler for the <see cref="ResetOrganizationSettingCommand" /> command.</summary>
/// <param name="settings">The settings stored for the organization of the scope.</param>
/// <param name="auditLog">The audit trail of the organization.</param>
/// <remarks>A setting already at its default changes nothing and records nothing.</remarks>
internal sealed class ResetOrganizationSettingCommandHandler(IOrganizationSettingRepository settings, IAuditLog auditLog)
  : ICommandHandler<ResetOrganizationSettingCommand>
{
  #region Fields

  /// <summary>The audit action of a setting of an organization put back to its default.</summary>
  internal const string AuditAction = "OrganizationSettings.Reset";

  #endregion

  #region Implementation of ICommandHandler<ResetOrganizationSettingCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The key is not a setting of an organization (<see cref="SettingErrors.Unknown" />).</exception>
  public async ValueTask<Unit> Handle(ResetOrganizationSettingCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var definition = SettingDefinitions.Find(command.Key);

    NotFoundException.ThrowIfNotOrganizationSetting(definition, command.Key);

    if (await settings.RemoveAsync(definition!, cancellationToken).ConfigureAwait(false))
    {
      await auditLog.RecordAsync(AuditAction, definition!.Key, null, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
