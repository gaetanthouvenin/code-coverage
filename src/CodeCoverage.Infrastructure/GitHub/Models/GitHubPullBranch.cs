// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPullBranch.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The head or the base of a pull request.</summary>
/// <param name="Ref">The name of the branch.</param>
/// <param name="Sha">The SHA of its commit.</param>
internal sealed record GitHubPullBranch(string Ref, string Sha);
