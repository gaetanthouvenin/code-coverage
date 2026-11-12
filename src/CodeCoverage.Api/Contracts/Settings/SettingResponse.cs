// ------------------------------------------------------------------------------------------------
// <copyright file="SettingResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Settings;

/// <summary>A setting of the instance; the value of a secret never leaves the server.</summary>
/// <param name="Key">The key of the setting, e.g. <c>Uploads:MaxSizeMb</c>.</param>
/// <param name="Group">The group of the setting, e.g. <c>Uploads</c>.</param>
/// <param name="Type">The type of its value.</param>
/// <param name="Value">Its effective value, or <see langword="null" /> for a secret or when none is defined.</param>
/// <param name="Default">The default of the catalog, or <see langword="null" />.</param>
/// <param name="Source">Where the value comes from: the environment, the settings saved from the console, or the default.</param>
/// <param name="IsSecret">Whether it is a secret, replaced through its own route and never read back.</param>
/// <param name="IsDefined">Whether a value is defined, a secret included.</param>
/// <param name="IsEditable">Whether it can be changed: a value from the environment is read-only.</param>
/// <param name="RequiresRestart">Whether a change takes effect only after a restart.</param>
/// <param name="UpdatedAt">When it was last saved from the console, or <see langword="null" />.</param>
/// <param name="UpdatedBy">Who last saved it, or <see langword="null" />.</param>
public sealed record SettingResponse(
  string Key,
  string Group,
  string Type,
  string? Value,
  string? Default,
  string Source,
  bool IsSecret,
  bool IsDefined,
  bool IsEditable,
  bool RequiresRestart,
  DateTimeOffset? UpdatedAt,
  string? UpdatedBy);
