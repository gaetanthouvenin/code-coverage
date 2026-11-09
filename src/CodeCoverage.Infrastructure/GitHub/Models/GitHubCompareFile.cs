// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCompareFile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A changed file in the answer of <c>GET …/compare/{base}...{head}</c>.</summary>
/// <param name="Filename">The path of the file.</param>
/// <param name="Status">How the file changed.</param>
/// <param name="Patch">The unified diff, left out by GitHub for a binary or too large a file.</param>
/// <param name="PreviousFilename">The path it had before a renaming, or <see langword="null" />.</param>
internal sealed record GitHubCompareFile(
  string Filename,
  string Status,
  string? Patch = null,
  string? PreviousFilename = null);
