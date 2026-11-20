// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveForgeWebhookCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Webhooks.Commands.ReceiveForgeWebhook;

/// <summary>
///   Stores a webhook delivery of a forge with connections and queues its job; authenticated by a secret of its
///   connection, in the handler.
/// </summary>
/// <param name="Forge">The forge of the route, never GitHub, whose deliveries keep their own reception.</param>
/// <param name="ConnectionId">The identifier of the connection, read from the address of the route, never from the body.</param>
/// <param name="DeliveryId">The identifier of the delivery, unique on its forge.</param>
/// <param name="Event">The name of the event, as the forge names it.</param>
/// <param name="Headers">The headers the validator of the forge reads, such as a signature.</param>
/// <param name="Payload">The raw body.</param>
/// <remarks>
///   Sent by the thin controller of each forge, on the route <c>/api/v1/{forge}/webhooks/{connectionId}</c> (ADR-017):
///   only the headers they read differ.
/// </remarks>
public sealed record ReceiveForgeWebhookCommand(
  Forge Forge,
  string ConnectionId,
  string DeliveryId,
  string Event,
  IReadOnlyDictionary<string, string> Headers,
  byte[] Payload) : ICommand<WebhookOutcome>, IAnonymousEntry;
