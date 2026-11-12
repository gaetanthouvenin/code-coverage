// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestSummary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;

namespace CodeCoverage.Domain.PullRequests;

/// <summary>A pull request with the comparison stored for its head, as a list shows it.</summary>
/// <param name="PullRequest">The pull request.</param>
/// <param name="Comparison">
///   What the comparison of its current head gives, or <see langword="null" /> while no notification compared it.
/// </param>
/// <remarks>A read model.</remarks>
public sealed record PullRequestSummary(PullRequest PullRequest, Comparison? Comparison);
