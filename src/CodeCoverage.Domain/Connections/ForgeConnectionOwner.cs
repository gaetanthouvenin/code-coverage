// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionOwner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Connections;

/// <summary>The organization of a forge connection and their states, as a webhook needs them before entering it.</summary>
/// <param name="OrganizationId">The identifier of the organization.</param>
/// <param name="Forge">The forge of the connection.</param>
/// <param name="State">The place of the organization in its lifecycle.</param>
/// <param name="Status">The health of the connection.</param>
public sealed record ForgeConnectionOwner(long OrganizationId, Forge Forge, OrganizationState State, ForgeConnectionStatus Status);
