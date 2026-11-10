// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardResolution.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Application.Services;

/// <summary>What the finalization of a commit inherits from its ancestors.</summary>
/// <param name="Sessions">The sessions carried forward.</param>
/// <param name="Status">What was found for the carried-forward flags.</param>
/// <param name="PendingAncestorIds">
///   The pending ancestors passed after the wait, the nearest first, when the status is <see cref="CarryforwardStatus.AncestorPending" />.
/// </param>
internal sealed record CarryforwardResolution(IReadOnlyList<SessionCoverage> Sessions, CarryforwardStatus Status, IReadOnlyList<long> PendingAncestorIds);
