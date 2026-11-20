// ------------------------------------------------------------------------------------------------
// <copyright file="RotateConnectionSecretCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Connections.Commands.RotateConnectionSecret;

/// <summary>Handler for the <see cref="RotateConnectionSecretCommand" /> command.</summary>
/// <param name="connections">The repository of the forge connections.</param>
/// <param name="forges">Resolves the connection verifier of the forge.</param>
/// <param name="protector">The secret protector, which protects the new secret before it reaches the connection.</param>
/// <param name="tokenGenerator">Draws the new secret of the webhooks.</param>
/// <param name="organization">The organization of the scope.</param>
/// <param name="auditLog">The audit trail, which never receives a secret.</param>
/// <remarks>
///   The connections are read from a global table: a connection of another organization is refused like an unknown
///   one. A new credential is kept only once its forge has verified that it reaches the account of the connection;
///   otherwise the old one stays. A new secret of the webhooks keeps the current one as the previous one, so that a
///   delivery signed before the forge learns the new one is still accepted.
/// </remarks>
internal sealed class RotateConnectionSecretCommandHandler(
  IForgeConnectionRepository connections,
  IForgeGatewayProvider forges,
  ISecretProtector protector,
  ITokenGenerator tokenGenerator,
  ICurrentOrganization organization,
  IAuditLog auditLog) : ICommandHandler<RotateConnectionSecretCommand>
{
  #region Methods

  /// <summary>Verifies the new credential against the forge, then puts it on the connection.</summary>
  /// <param name="connection">The connection of the organization of the scope.</param>
  /// <param name="command">The command, which carries the new credential.</param>
  /// <param name="cancellationToken">A token to cancel the verification.</param>
  /// <returns>A task that completes when the connection holds the new credential.</returns>
  /// <exception cref="UnprocessableEntityException">The forge refuses the credential, or it reaches another account.</exception>
  private async Task RotateCredentialAsync(ForgeConnection connection, RotateConnectionSecretCommand command, CancellationToken cancellationToken)
  {
    var credential = command.Credential ?? string.Empty;
    var request = new ForgeConnectionRequest(connection.Forge, connection.Kind, command.Login, connection.TenantId, credential);
    var check = await forges.GetVerifier(connection.Forge).VerifyAsync(request, cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfCredentialRefused(check.IsRefused, check.Refusal, connection.Forge, command.Login);

    var account = check.Account;
    var reached = Organization.NormalizeExternalAccountId(account.ExternalAccountId);

    UnprocessableEntityException.ThrowIfCredentialRefused(
      !string.Equals(reached, connection.ExternalAccountId, StringComparison.Ordinal),
      "the credential reaches another account.",
      connection.Forge,
      command.Login
    );

    connection.RotateCredential(protector.Protect(credential), account.ServiceAccountExternalId, account.ExpiresAt);
  }

  #endregion

  #region Implementation of ICommandHandler<RotateConnectionSecretCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The organization of the scope has no such connection.</exception>
  /// <exception cref="UnprocessableEntityException">The forge refuses the new credential, or it reaches another account.</exception>
  public async ValueTask<Unit> Handle(RotateConnectionSecretCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var connection = await connections.FindAsync(command.ConnectionId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfNotInOrganization(connection, organization.Id);

    string action;

    if (command.Kind is ConnectionSecretKind.WebhookSecret)
    {
      // The plain secret never leaves this statement: the forge learns it when its hooks are set again.
      connection.RotateWebhookSecret(protector.Protect(tokenGenerator.NewWebhookSecret()));
      action = "Connection.RotateWebhookSecret";
    }
    else
    {
      await RotateCredentialAsync(connection, command, cancellationToken).ConfigureAwait(false);
      action = "Connection.RotateCredential";
    }

    await connections.UpdateAsync(connection, cancellationToken).ConfigureAwait(false);

    var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["ConnectionId"] = connection.Id };

    await auditLog.RecordAsync(action, $"{command.Forge}/{command.Login}", details, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
