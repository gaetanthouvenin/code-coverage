// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingsSnapshot.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>The effective values of the settings of one organization, read at one moment.</summary>
/// <param name="StatusContextPrefix">The prefix of the commit status contexts.</param>
/// <param name="StatusesEnabled">Whether the commit statuses are sent.</param>
/// <param name="CommentsEnabled">Whether the pull request comments are sent.</param>
/// <param name="OrganizationYaml">
///   The YAML configuration shared by every repository of the organization, or <see langword="null" /> when there is none.
/// </param>
/// <param name="ChecksEnabled">
///   Whether GitHub Check Runs replace the commit statuses (<c>Notifications:ChecksEnabled</c>, off by default).
/// </param>
public sealed record OrganizationSettingsSnapshot(
  string StatusContextPrefix,
  bool StatusesEnabled,
  bool CommentsEnabled,
  string? OrganizationYaml,
  bool ChecksEnabled = false);
