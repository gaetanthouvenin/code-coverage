// ------------------------------------------------------------------------------------------------
// <copyright file="HandleWebhookDeliveryCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.HandleWebhookDelivery;

/// <summary>Handles a delivery of GitHub that was stored at its reception: the work of the <c>HandleWebhook</c> job.</summary>
/// <param name="DeliveryId">The identifier of the delivery.</param>
/// <remarks>
///   Runs in the organization of the delivery, or on the lane of the instance for the installation events
///   of an account that is not an active organization: nothing is then read or written in an organization.
/// </remarks>
public sealed record HandleWebhookDeliveryCommand(string DeliveryId) : ICommand, ISystemOnly;