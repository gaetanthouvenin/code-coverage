// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsTree.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The answer of <c>GET _apis/git/repositories/{repositoryId}/trees/{sha1}</c>.</summary>
/// <param name="ObjectId">The SHA of the tree that was read.</param>
/// <param name="TreeEntries">
///   Its entries; when read recursively, those of its subdirectories as well, each path relative to the tree that was
///   read.
/// </param>
/// <remarks>Azure DevOps never cuts the entries of a tree: the answer reports no truncation.</remarks>
internal sealed record AzureDevOpsTree(string ObjectId, IReadOnlyList<AzureDevOpsTreeEntry> TreeEntries);
