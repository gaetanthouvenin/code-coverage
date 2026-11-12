// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateOrganizationSettingCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Organizations.Commands.UpdateOrganizationSetting;

/// <summary>Handler for the <see cref="UpdateOrganizationSettingCommand" /> command.</summary>
/// <param name="settings">The settings stored for the organization of the scope.</param>
/// <param name="currentUser">The principal of the scope, who is audited.</param>
/// <param name="auditLog">The audit trail of the organization.</param>
/// <param name="clock">The clock.</param>
/// <remarks>It writes the organization of the scope only, which the gate entered from the address.</remarks>
internal sealed class UpdateOrganizationSettingCommandHandler(
  IOrganizationSettingRepository settings,
  ICurrentUser currentUser,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<UpdateOrganizationSettingCommand>
{
  #region Fields

  /// <summary>The audit action of a changed setting of an organization.</summary>
  internal const string AuditAction = "OrganizationSettings.Update";

  #endregion

  #region Implementation of ICommandHandler<UpdateOrganizationSettingCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The key is not a setting of an organization (<see cref="SettingErrors.Unknown" />).</exception>
  public async ValueTask<Unit> Handle(UpdateOrganizationSettingCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var definition = SettingDefinitions.Find(command.Key);

    NotFoundException.ThrowIfNotOrganizationSetting(definition, command.Key);

    var value = SettingRules.Normalize(definition!, command.Value);

    await settings.SaveAsync(definition!, value, currentUser.GetActorName(), clock.UtcNow, cancellationToken).ConfigureAwait(false);

    var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["Value"] = value };

    await auditLog.RecordAsync(AuditAction, definition!.Key, details, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
