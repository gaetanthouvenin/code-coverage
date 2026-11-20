// ------------------------------------------------------------------------------------------------
// <copyright file="HandleForgeDeliveryCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Webhooks.Commands.HandleForgeDelivery;

/// <summary>
///   Handles a delivery of a forge with connections that was stored at its reception: the work of the
///   <c>HandleForgeDelivery</c> job, for any forge but GitHub.
/// </summary>
/// <param name="Forge">The forge that sent the delivery.</param>
/// <param name="DeliveryId">The identifier of the delivery on its forge.</param>
/// <remarks>Runs in the organization of the connection that received the delivery.</remarks>
public sealed record HandleForgeDeliveryCommand(Forge Forge, string DeliveryId) : ICommand, ISystemOnly;
