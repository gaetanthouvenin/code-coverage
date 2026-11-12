// ------------------------------------------------------------------------------------------------
// <copyright file="SettingViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>A setting of the instance, as its row shows it.</summary>
/// <param name="Key">The key of the setting, e.g. <c>Uploads:MaxFilesPerUpload</c>.</param>
/// <param name="Kind">How it is edited.</param>
/// <param name="Value">Its effective value; always <see langword="null" /> for a secret.</param>
/// <param name="Origin">Where its value comes from.</param>
/// <param name="IsDefined">Whether a value is defined, a secret included.</param>
/// <param name="IsEditable">Whether it can be changed: a value from the environment is read-only.</param>
/// <param name="RequiresRestart">Whether a change takes effect only after a restart.</param>
public sealed record SettingViewModel(
  string Key,
  SettingEditorKind Kind,
  string? Value,
  SettingOrigin Origin,
  bool IsDefined,
  bool IsEditable,
  bool RequiresRestart)
{
  #region Properties

  /// <summary>Gets a value indicating whether the value saved from the console can be removed.</summary>
  public bool CanReset => IsEditable && Origin is SettingOrigin.Database or SettingOrigin.Organization;

  #endregion
}
