// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubTreeEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>An entry of the answer of <c>GET …/git/trees/{sha}</c>.</summary>
/// <param name="Path">The path of the entry, relative to the root of the tree.</param>
/// <param name="Type">The kind of entry: <c>blob</c>, <c>tree</c>, or <c>commit</c> for a submodule.</param>
/// <param name="Sha">The SHA of the object.</param>
internal sealed record GitHubTreeEntry(string Path, string Type, string Sha);
