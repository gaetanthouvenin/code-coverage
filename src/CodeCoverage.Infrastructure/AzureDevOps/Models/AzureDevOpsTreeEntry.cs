// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsTreeEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>An entry of a tree, as listed by <c>GET _apis/git/repositories/{repositoryId}/trees/{sha1}</c>.</summary>
/// <param name="ObjectId">The SHA of the blob of a file, of the tree of a directory, or of the commit of a submodule.</param>
/// <param name="RelativePath">The path of the entry, relative to the tree that was read, <c>src/app.cs</c>.</param>
/// <param name="GitObjectType">The type of the entry: <c>blob</c>, <c>tree</c>, or <c>commit</c> for a submodule.</param>
internal sealed record AzureDevOpsTreeEntry(string ObjectId, string RelativePath, string GitObjectType);
