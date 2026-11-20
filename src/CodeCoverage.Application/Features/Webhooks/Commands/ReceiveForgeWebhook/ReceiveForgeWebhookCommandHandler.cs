// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveForgeWebhookCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text.Json;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Webhooks.Commands.ReceiveForgeWebhook;

/// <summary>Handler for the <see cref="ReceiveForgeWebhookCommand" /> command.</summary>
/// <param name="connections">The connections of the organizations to their forges.</param>
/// <param name="forges">The adapters of the forges, which give the webhook validator of the forge of the route.</param>
/// <param name="protector">What decrypts the webhook secrets of the connection.</param>
/// <param name="deliveries">The webhook deliveries.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The route <c>/api/v1/{forge}/webhooks/{connectionId}</c> names the connection (ADR-017). It is read once, from
///     the global table <c>ForgeConnections</c>, before any scope has entered an organization, then the validator of the
///     forge checks the delivery against its current or previous secret, in constant time. The delivery is stored and
///     its <see cref="JobTypes.HandleForgeDelivery" /> job queued by <see cref="JobRequest.ForOrganization" />, in one
///     transaction: the body is read by the job, never here, and nothing here calls the forge.
///   </para>
///   <para>
///     An unknown connection, a connection of another forge and a delivery no secret authenticates get the same
///     refusal, <see cref="WebhookErrors.InvalidSignature" />: the route is not an oracle of the connections. An
///     authenticated delivery of an organization that is not active, or of a suspended connection, is not stored.
///   </para>
/// </remarks>
internal sealed class ReceiveForgeWebhookCommandHandler(
  IForgeConnectionRepository connections,
  IForgeGatewayProvider forges,
  ISecretProtector protector,
  IWebhookDeliveryRepository deliveries,
  IClock clock) : ICommandHandler<ReceiveForgeWebhookCommand, WebhookOutcome>
{
  #region Methods

  /// <summary>Tells whether a body is a JSON object, as every forge with connections sends.</summary>
  /// <param name="payload">The raw body.</param>
  /// <returns><see langword="true" /> when the body is a JSON object.</returns>
  private static bool IsJsonObject(byte[] payload)
  {
    try
    {
      using var document = JsonDocument.Parse(payload);

      return document.RootElement.ValueKind is JsonValueKind.Object;
    }
    catch (JsonException)
    {
      return false;
    }
  }

  /// <summary>Decrypts a webhook secret and adds it to the secrets to check, unless it cannot be decrypted.</summary>
  /// <param name="secrets">The secrets to check.</param>
  /// <param name="protectedSecret">The encrypted secret.</param>
  /// <remarks>
  ///   An altered value or a lost key ring skips the secret: the other secret of the connection may still authenticate
  ///   the delivery, and the anonymous route never answers a server error that only an existing connection could cause.
  /// </remarks>
  private void TryAddSecret(List<string> secrets, string protectedSecret)
  {
    try
    {
      secrets.Add(protector.Unprotect(protectedSecret));
    }
    catch (CryptographicException)
    {
      // The secret cannot be decrypted: it authenticates nothing.
    }
  }

  /// <summary>Finds the connection of the route and checks the delivery against its secrets.</summary>
  /// <param name="command">The command.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The owner of the connection, or <see langword="null" /> when the connection is unknown, belongs to another forge,
  ///   or no secret of it authenticates the delivery.
  /// </returns>
  private async Task<ForgeConnectionOwner?> AuthenticateAsync(ReceiveForgeWebhookCommand command, CancellationToken cancellationToken)
  {
    // An identifier that cannot be one is refused like an unknown connection, without reading the table.
    var owner = ForgeConnection.IsWellFormedId(command.ConnectionId)
      ? await connections.FindOwnerAsync(command.ConnectionId, cancellationToken).ConfigureAwait(false)
      : null;

    if (owner is null || owner.Forge != command.Forge)
    {
      return null;
    }

    List<string> secrets = [];
    TryAddSecret(secrets, owner.WebhookSecretProtected);

    if (owner.WebhookSecretPreviousProtected is { } previous)
    {
      TryAddSecret(secrets, previous);
    }

    // No secret could be decrypted: the same refusal as a wrong signature, never a server error that tells the
    // connection exists.
    if (secrets.Count == 0)
    {
      return null;
    }

    var request = new ForgeWebhookRequest(command.Forge, command.ConnectionId, command.DeliveryId, command.Event, command.Headers, command.Payload);

    return forges.GetWebhookValidator(command.Forge).IsValid(request, secrets) ? owner : null;
  }

  #endregion

  #region Implementation of ICommandHandler<ReceiveForgeWebhookCommand,WebhookOutcome>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">
  ///   The connection is unknown or belongs to another forge, or no secret of it authenticates the delivery.
  /// </exception>
  /// <exception cref="BadRequestException">The body is not a JSON object.</exception>
  /// <exception cref="NotSupportedException">No webhook validator is implemented for the forge of the route.</exception>
  public async ValueTask<WebhookOutcome> Handle(ReceiveForgeWebhookCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var owner = await AuthenticateAsync(command, cancellationToken).ConfigureAwait(false);

    UnauthorizedException.ThrowIfForgeWebhookUnauthenticated(owner);
    BadRequestException.ThrowIfForgeWebhookPayloadInvalid(IsJsonObject(command.Payload));

    if (owner.State is not OrganizationState.Active || owner.Status is ForgeConnectionStatus.Suspended)
    {
      return WebhookOutcome.Dropped;
    }

    var delivery = new ForgeDelivery(command.Forge, command.DeliveryId, command.Event, null, command.ConnectionId, null, command.Payload);
    var job = JobRequest.ForOrganization(
      owner.OrganizationId,
      JobTypes.HandleForgeDelivery,
      ForgeDeliveryJobs.Payload(command.Forge, command.DeliveryId),
      RepositoryReconciliationJobs.ConnectionKey(command.ConnectionId)
    );

    return await deliveries.TryStoreForgeDeliveryAsync(delivery, job, clock.UtcNow, cancellationToken).ConfigureAwait(false)
      ? WebhookOutcome.Accepted
      : WebhookOutcome.Duplicate;
  }

  #endregion
}
