// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a delivery of a forge reports once its translator has read it, whatever the forge.</summary>
/// <remarks>
///   The use case that handles the deliveries knows <see cref="ForgePushEvent" />, <see cref="ForgePullRequestEvent" />
///   and <see cref="ForgeRepositoryEvent" />; any other event is recorded as handled, never as a failure.
/// </remarks>
public abstract record ForgeEvent;
