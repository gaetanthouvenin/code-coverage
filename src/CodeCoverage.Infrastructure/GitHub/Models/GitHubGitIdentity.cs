// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubGitIdentity.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The author or the committer recorded in a git commit.</summary>
/// <param name="Date">The date of the signature, or <see langword="null" /> when GitHub leaves it out.</param>
internal sealed record GitHubGitIdentity(DateTimeOffset? Date = null);
