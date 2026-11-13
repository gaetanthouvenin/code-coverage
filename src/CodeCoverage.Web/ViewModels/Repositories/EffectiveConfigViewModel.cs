// ------------------------------------------------------------------------------------------------
// <copyright file="EffectiveConfigViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>The configuration in force for a repository, as the settings page shows it: read-only, as it is written.</summary>
/// <param name="Source">The key of its source in the resources (<c>ConfigSource_{name}</c>).</param>
/// <param name="Path">The file it was read from, or <see langword="null" />.</param>
/// <param name="Sha">The short SHA of the commit it was resolved at, or <see langword="null" />.</param>
/// <param name="Yaml">The YAML of the repository, as written, or <see langword="null" /> when none was found.</param>
/// <param name="Warnings">What was not applied as written.</param>
/// <param name="Errors">Why a file was refused, as the parser wrote it.</param>
public sealed record EffectiveConfigViewModel(
  string Source,
  string? Path,
  string? Sha,
  string? Yaml,
  IReadOnlyList<ConfigWarningViewModel> Warnings,
  IReadOnlyList<string> Errors)
{
  #region Properties

  /// <summary>Gets a value indicating whether a file was refused on the way to this configuration.</summary>
  public bool IsValid => Errors.Count == 0;

  #endregion
}
