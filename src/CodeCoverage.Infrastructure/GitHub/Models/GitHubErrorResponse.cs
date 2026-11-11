// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubErrorResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The body of a refusal of the GitHub API.</summary>
/// <param name="Message">What GitHub says of the refusal, or <see langword="null" />.</param>
internal sealed record GitHubErrorResponse(string? Message = null);
