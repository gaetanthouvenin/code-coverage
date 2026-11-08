// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeTreeEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A file or a directory of a tree; submodules are left out.</summary>
/// <param name="Path">The path of the entry, relative to the root of the tree that was read.</param>
/// <param name="Sha">The SHA of the blob of a file, or of the tree of a directory.</param>
/// <param name="IsDirectory">A value indicating whether the entry is a directory.</param>
public sealed record ForgeTreeEntry(string Path, GitSha Sha, bool IsDirectory);
