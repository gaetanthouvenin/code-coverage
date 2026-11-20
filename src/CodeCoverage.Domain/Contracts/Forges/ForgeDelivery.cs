// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeDelivery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A webhook delivery of a forge with connections, as it was stored at its reception.</summary>
/// <remarks>
///   Only the translator of its forge reads the body: the use case that handles the delivery sees the
///   <see cref="ForgeEvent" /> it gives.
/// </remarks>
public sealed class ForgeDelivery
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeDelivery" /> class.</summary>
  /// <param name="forge">The forge that sent the delivery.</param>
  /// <param name="deliveryId">The identifier of the delivery, unique on its forge.</param>
  /// <param name="event">The name of the event.</param>
  /// <param name="action">The action of the event, or <see langword="null" />.</param>
  /// <param name="connectionId">The connection whose webhook received the delivery.</param>
  /// <param name="repositoryExternalId">The identifier of the repository on its forge, in text, or <see langword="null" />.</param>
  /// <param name="payload">The raw body.</param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="deliveryId" />, <paramref name="event" />, <paramref name="connectionId" /> or
  ///   <paramref name="payload" /> is <see langword="null" />.
  /// </exception>
  public ForgeDelivery(
    Forge forge,
    string deliveryId,
    string @event,
    string? action,
    string connectionId,
    string? repositoryExternalId,
    byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(deliveryId);
    ArgumentNullException.ThrowIfNull(@event);
    ArgumentNullException.ThrowIfNull(connectionId);
    ArgumentNullException.ThrowIfNull(payload);

    Forge = forge;
    DeliveryId = deliveryId;
    Event = @event;
    Action = action;
    ConnectionId = connectionId;
    RepositoryExternalId = repositoryExternalId;
    Payload = payload;
  }

  #endregion

  #region Properties

  /// <summary>Gets the forge that sent the delivery.</summary>
  public Forge Forge { get; }

  /// <summary>Gets the identifier of the delivery, the same when the forge delivers it again.</summary>
  public string DeliveryId { get; }

  /// <summary>Gets the name of the event, as the forge names it.</summary>
  public string Event { get; }

  /// <summary>Gets the action of the event, or <see langword="null" /> for an event without one.</summary>
  public string? Action { get; }

  /// <summary>Gets the identifier of the connection whose webhook received the delivery (ADR-017).</summary>
  public string ConnectionId { get; }

  /// <summary>Gets the identifier of the repository on its forge, in text, or <see langword="null" />.</summary>
  public string? RepositoryExternalId { get; }

  /// <summary>Gets the raw body of the delivery, as it was authenticated.</summary>
  public byte[] Payload { get; }

  #endregion
}
