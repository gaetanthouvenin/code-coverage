// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubStatusRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The body of <c>POST /repositories/{id}/statuses/{sha}</c>.</summary>
/// <param name="State">The state: <c>success</c>, <c>failure</c> or <c>error</c>.</param>
/// <param name="Context">The context of the status.</param>
/// <param name="Description">The description, 140 characters at most.</param>
/// <param name="TargetUrl">The address the status links to, or <see langword="null" />.</param>
internal sealed record GitHubStatusRequest(string State, string Context, string Description, string? TargetUrl);