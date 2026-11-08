// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeChangedFile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A file changed between two commits.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="PreviousPath">The path it had before a renaming, or <see langword="null" />.</param>
/// <param name="Status">How the file changed.</param>
/// <param name="Patch">
///   The unified diff of the file, or <see langword="null" /> when the forge did not provide it: an unknown
///   patch is never an empty one.
/// </param>
public sealed record ForgeChangedFile(string Path, string? PreviousPath, ForgeChangedFileStatus Status, string? Patch);
