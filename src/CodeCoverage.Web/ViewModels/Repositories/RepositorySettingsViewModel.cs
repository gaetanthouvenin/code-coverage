// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySettingsViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>What the settings page of a repository shows: its state and the configuration in force.</summary>
/// <param name="Name">The name of the repository on its forge.</param>
/// <param name="IsActive">Whether the repository accepts uploads and publishes; false once an administrator disabled it.</param>
/// <param name="Config">The configuration in force, as it is written.</param>
/// <param name="Tokens">The upload tokens of the repository, the revoked ones included.</param>
/// <param name="BadgeToken">The badge token, or <see langword="null" /> for a public repository.</param>
/// <remarks>The badge token is a value: an instance never writes it down.</remarks>
public sealed record RepositorySettingsViewModel(
  string Name,
  bool IsActive,
  EffectiveConfigViewModel Config,
  IReadOnlyList<UploadTokenViewModel> Tokens,
  string? BadgeToken)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the badge token: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(RepositorySettingsViewModel)} {{ Name = {Name}, IsActive = {IsActive} }}";

  #endregion
}
