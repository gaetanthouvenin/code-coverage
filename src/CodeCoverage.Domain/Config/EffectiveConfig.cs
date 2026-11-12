// ------------------------------------------------------------------------------------------------
// <copyright file="EffectiveConfig.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Config;

/// <summary>The effective configuration of a repository, as a read of the API shows it.</summary>
/// <param name="Config">The merged layers: the defaults, the organization, then the repository.</param>
/// <param name="Source">Where the configuration of the repository came from.</param>
/// <param name="Sha">
///   The commit the configuration was resolved at: the covered head of the default branch when its configuration was
///   kept; <see langword="null" /> when it was not resolved at a commit and the YAML of the default branch was read
///   instead.
/// </param>
/// <param name="Path">The path of the file of the commit, or <see langword="null" />.</param>
/// <param name="Yaml">The YAML of the repository that was found, as written, or <see langword="null" />.</param>
/// <param name="Warnings">What was not applied as written.</param>
/// <param name="Errors">Why a file was refused on the way to this source.</param>
/// <remarks>A read model: reading it calls no forge and writes nothing.</remarks>
public sealed record EffectiveConfig(
  CoverageConfig Config,
  ConfigSource Source,
  GitSha? Sha,
  string? Path,
  string? Yaml,
  IReadOnlyList<ConfigWarning> Warnings,
  IReadOnlyList<string> Errors)
{
  #region Properties

  /// <summary>Gets a value indicating whether the configuration was resolved at a commit.</summary>
  public bool IsResolvedAtSha => Sha is not null;

  #endregion
}
