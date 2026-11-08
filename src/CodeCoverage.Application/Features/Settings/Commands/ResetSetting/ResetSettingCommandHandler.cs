// ------------------------------------------------------------------------------------------------
// <copyright file="ResetSettingCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Settings.Commands.ResetSetting;

/// <summary>Handler for the <see cref="ResetSettingCommand" /> command.</summary>
/// <param name="store">The settings store.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class ResetSettingCommandHandler(ISettingsStore store, IAuditLog auditLog)
  : ICommandHandler<ResetSettingCommand>
{
  #region Fields

  /// <summary>The audit action of a reset setting.</summary>
  internal const string AuditAction = "Settings.Reset";

  #endregion

  #region Implementation of ICommandHandler<ResetSettingCommand,Unit>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The key is not in the catalog (<see cref="SettingErrors.Unknown" />).</exception>
  /// <exception cref="UnprocessableEntityException">
  ///   A stronger source provides the value (<see cref="SettingErrors.ProvidedByEnvironment" />).
  /// </exception>
  public async ValueTask<Unit> Handle(ResetSettingCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var definition = SettingRules.FindOrThrow(command.Key);
    var previous = await SettingRules.GetEditableAsync(store, definition, cancellationToken)
                                     .ConfigureAwait(false);

    await store.ResetAsync(definition, cancellationToken).ConfigureAwait(false);

    // The previous value of a secret is never recorded, not even protected.
    var details = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["PreviousSource"] = previous.Source.ToString(),
    };

    if (!definition.IsSecret)
    {
      details["PreviousValue"] = previous.Value ?? string.Empty;
    }

    await auditLog.RecordAsync(AuditAction, definition.Key, details, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
