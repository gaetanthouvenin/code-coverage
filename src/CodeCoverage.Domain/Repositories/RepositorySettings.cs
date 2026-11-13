// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySettings.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Repositories;

/// <summary>What the settings page of a repository shows: the row of the repository and the configuration in force.</summary>
/// <param name="Repository">The repository, with the moment an administrator disabled it.</param>
/// <param name="Config">The configuration in force, with its source, its path, its SHA, its warnings and its errors.</param>
public sealed record RepositorySettings(TrackedRepository Repository, EffectiveConfig Config);
