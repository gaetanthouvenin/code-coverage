// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeApiTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Access.Commands.RevokeApiToken;

/// <summary>Handler for the <see cref="RevokeApiTokenCommand" /> command.</summary>
/// <param name="currentUser">The principal of the scope.</param>
/// <param name="tokens">The repository of the personal tokens.</param>
/// <param name="auditLog">The audit trail.</param>
/// <param name="clock">The clock.</param>
/// <remarks>Revoking a revoked token changes nothing and records nothing.</remarks>
internal sealed class RevokeApiTokenCommandHandler(
  ICurrentUser currentUser,
  IApiTokenRepository tokens,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<RevokeApiTokenCommand>
{
  #region Fields

  /// <summary>The audit action of a revoked token.</summary>
  internal const string AuditAction = "ApiTokens.Revoke";

  #endregion

  #region Implementation of ICommandHandler<RevokeApiTokenCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The user owns no such token, or it is out of reach (<see cref="AccessErrors.ApiTokenNotFound" />).</exception>
  public async ValueTask<Unit> Handle(RevokeApiTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var userId = currentUser.UserId;

    UnauthorizedException.ThrowIfNoUser(userId);

    var owned = await tokens.FindOfUserAsync(userId.Value, command.Id, cancellationToken).ConfigureAwait(false);
    var token = owned is not null && currentUser.Reaches(owned) ? owned : null;

    NotFoundException.ThrowIfApiTokenNotFound(token);

    if (token.RevokedAt is not null)
    {
      return Unit.Value;
    }

    token.Revoke(clock.UtcNow);
    await tokens.UpdateAsync(token, cancellationToken).ConfigureAwait(false);
    // A token bound to an organization is an event of that organization, never of the instance (fail-closed).
    var target = token.Id.ToString(CultureInfo.InvariantCulture);

    if (token.OrganizationId is { } organization)
    {
      await auditLog.RecordAsync(organization, AuditAction, target, null, cancellationToken).ConfigureAwait(false);
    }
    else
    {
      await auditLog.RecordAsync(AuditAction, target, null, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
