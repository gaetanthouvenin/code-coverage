// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeTree.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge reports about the content of a directory at a commit.</summary>
/// <param name="Sha">The SHA that was read.</param>
/// <param name="Entries">The entries that the forge returned.</param>
/// <param name="Truncated">A value indicating whether the forge left out some of the entries.</param>
public sealed record ForgeTree(GitSha Sha, IReadOnlyList<ForgeTreeEntry> Entries, bool Truncated);
