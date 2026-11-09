// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCommitRef.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A reference to a commit, as a parent or as a merge base.</summary>
/// <param name="Sha">The SHA of the commit.</param>
internal sealed record GitHubCommitRef(string Sha);
