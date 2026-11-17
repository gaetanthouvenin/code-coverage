// ------------------------------------------------------------------------------------------------
// <copyright file="ForgePullRequestEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A change of a pull request that is worth a synchronization, as any forge reports it once its delivery is translated.</summary>
/// <param name="RepositoryExternalId">The identifier the forge gives the repository of the pull request.</param>
/// <param name="Number">The number of the pull request in its repository.</param>
/// <remarks>
///   Only the pull request is named: its state, its head and its base always come from the forge, which is the
///   reference. A change that records nothing, a label or a review request for instance, is never translated.
/// </remarks>
public sealed record ForgePullRequestEvent(string RepositoryExternalId, int Number);
