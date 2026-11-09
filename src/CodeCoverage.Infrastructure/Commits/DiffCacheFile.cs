// ------------------------------------------------------------------------------------------------
// <copyright file="DiffCacheFile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>A file of a cached diff, as JSON.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="PreviousPath">The path it had before a renaming, or <see langword="null" />.</param>
/// <param name="Status">How the file changed, by name.</param>
/// <param name="Patch">The unified diff, or <see langword="null" /> when it is not known.</param>
internal sealed record DiffCacheFile(string Path, string? PreviousPath, string Status, string? Patch);
