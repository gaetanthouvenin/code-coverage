// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveWebhookCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.ReceiveWebhook;

/// <summary>Stores a webhook delivery of GitHub and queues its job; authenticated by its signature, in the handler.</summary>
/// <param name="DeliveryId">The value of the <c>X-GitHub-Delivery</c> header.</param>
/// <param name="Event">The value of the <c>X-GitHub-Event</c> header.</param>
/// <param name="Signature">The value of the <c>X-Hub-Signature-256</c> header, or <see langword="null" />.</param>
/// <param name="Payload">The raw body.</param>
public sealed record ReceiveWebhookCommand(string DeliveryId, string Event, string? Signature, byte[] Payload)
  : ICommand<WebhookOutcome>, IAnonymousEntry;
