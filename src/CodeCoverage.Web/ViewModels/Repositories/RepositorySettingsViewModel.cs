// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySettingsViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>What the settings page of a repository shows: its state and the configuration in force.</summary>
/// <param name="Name">The name of the repository on its forge.</param>
/// <param name="IsActive">Whether the repository accepts uploads and publishes; false once an administrator disabled it.</param>
/// <param name="Config">The configuration in force, as it is written.</param>
public sealed record RepositorySettingsViewModel(string Name, bool IsActive, EffectiveConfigViewModel Config);
