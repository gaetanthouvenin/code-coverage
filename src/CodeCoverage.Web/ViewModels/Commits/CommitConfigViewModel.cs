// ------------------------------------------------------------------------------------------------
// <copyright file="CommitConfigViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The configuration that was really applied to a commit, as stored when it was processed.</summary>
/// <param name="Source">The key of its source in the resources (<c>ConfigSource_{name}</c>), or <see langword="null" /> when only the raw source of the commit is known.</param>
/// <param name="RawSource">The source recorded on the commit, shown when no configuration row is stored.</param>
/// <param name="Path">The file it was read from, or <see langword="null" />.</param>
/// <param name="Warnings">The warnings raised while it was read.</param>
/// <param name="Errors">The errors that made the file unreadable, as the parser wrote them.</param>
public sealed record CommitConfigViewModel(
  string? Source,
  string? RawSource,
  string? Path,
  IReadOnlyList<ConfigWarningViewModel> Warnings,
  IReadOnlyList<string> Errors);
