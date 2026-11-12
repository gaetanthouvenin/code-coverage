// ------------------------------------------------------------------------------------------------
// <copyright file="EffectiveConfigResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Api.Contracts.Configuration;

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>The effective configuration of a repository, at the covered head of its default branch.</summary>
/// <param name="Source">Where the configuration of the repository came from: commit, repository default, organization or built-in.</param>
/// <param name="Sha">The commit it was resolved at, or <see langword="null" /> when it was not resolved at a commit.</param>
/// <param name="IsResolvedAtSha">
///   Whether it was resolved at the covered head of the default branch; otherwise the YAML of the default branch and of
///   the organization were merged, which a commit may not use as is.
/// </param>
/// <param name="Path">The path of the file of the commit, or <see langword="null" />.</param>
/// <param name="Yaml">The YAML of the repository that was found, as written, or <see langword="null" />.</param>
/// <param name="Config">The merged configuration (defaults, organization, repository), as JSON.</param>
/// <param name="Warnings">What was not applied as written.</param>
/// <param name="Errors">Why a file was refused on the way to this source.</param>
public sealed record EffectiveConfigResponse(
  string Source,
  string? Sha,
  bool IsResolvedAtSha,
  string? Path,
  string? Yaml,
  JsonElement Config,
  IReadOnlyList<ConfigWarningModel> Warnings,
  IReadOnlyList<string> Errors);
