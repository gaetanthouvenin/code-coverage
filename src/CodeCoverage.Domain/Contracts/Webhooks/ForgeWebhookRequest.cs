// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeWebhookRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Webhooks;

/// <summary>A webhook delivery of a forge with connections, as its route received it and before it is authenticated.</summary>
/// <remarks>
///   The connection comes from the address of the route, never from the body (ADR-017): the body is not trusted until
///   the validator of the forge authenticates it with a secret of that connection.
/// </remarks>
public sealed class ForgeWebhookRequest
{
  #region Fields

  /// <summary>The longest identifier of a delivery, the size of its column.</summary>
  public const int DeliveryIdMaxLength = 64;

  /// <summary>The longest name of an event, the size of its column.</summary>
  public const int EventMaxLength = 64;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeWebhookRequest" /> class.</summary>
  /// <param name="forge">The forge of the route that received the delivery.</param>
  /// <param name="connectionId">The identifier of the connection, read from the address of the route.</param>
  /// <param name="deliveryId">The identifier of the delivery, unique on its forge.</param>
  /// <param name="event">The name of the event, as the forge names it.</param>
  /// <param name="headers">The headers the validator of the forge reads, such as a signature; their names ignore the case.</param>
  /// <param name="payload">The raw body.</param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="connectionId" />, <paramref name="deliveryId" />, <paramref name="event" />,
  ///   <paramref name="headers" /> or <paramref name="payload" /> is <see langword="null" />.
  /// </exception>
  public ForgeWebhookRequest(
    Forge forge,
    string connectionId,
    string deliveryId,
    string @event,
    IReadOnlyDictionary<string, string> headers,
    byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(connectionId);
    ArgumentNullException.ThrowIfNull(deliveryId);
    ArgumentNullException.ThrowIfNull(@event);
    ArgumentNullException.ThrowIfNull(headers);
    ArgumentNullException.ThrowIfNull(payload);

    Forge = forge;
    ConnectionId = connectionId;
    DeliveryId = deliveryId;
    Event = @event;
    Headers = headers.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    Payload = payload;
  }

  #endregion

  #region Properties

  /// <summary>Gets the forge of the route that received the delivery.</summary>
  public Forge Forge { get; }

  /// <summary>Gets the identifier of the connection, read from the address of the route.</summary>
  public string ConnectionId { get; }

  /// <summary>Gets the identifier of the delivery, the same when the forge delivers it again.</summary>
  public string DeliveryId { get; }

  /// <summary>Gets the name of the event, as the forge names it.</summary>
  public string Event { get; }

  /// <summary>Gets the headers the validator of the forge reads; their names ignore the case.</summary>
  public IReadOnlyDictionary<string, string> Headers { get; }

  /// <summary>Gets the raw body, exactly as it was received.</summary>
  public byte[] Payload { get; }

  #endregion
}
