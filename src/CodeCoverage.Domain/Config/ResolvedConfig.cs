// ------------------------------------------------------------------------------------------------
// <copyright file="ResolvedConfig.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>The effective configuration of a commit.</summary>
/// <param name="Config">The merged layers.</param>
/// <param name="Source">Where the configuration of the repository came from.</param>
/// <param name="Warnings">What was not applied as written.</param>
/// <param name="Errors">Why a file was refused on the way to this source.</param>
public sealed record ResolvedConfig(
  CoverageConfig Config,
  ConfigSource Source,
  IReadOnlyList<ConfigWarning> Warnings,
  IReadOnlyList<string> Errors);
