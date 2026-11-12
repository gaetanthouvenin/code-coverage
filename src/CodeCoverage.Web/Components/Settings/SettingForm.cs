// ------------------------------------------------------------------------------------------------
// <copyright file="SettingForm.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Settings;

/// <summary>The value typed in the row of a setting: the validation of the server reports on <see cref="Value" />.</summary>
public sealed class SettingForm
{
  #region Properties

  /// <summary>Gets or sets the value typed.</summary>
  public string Value { get; set; } = string.Empty;

  #endregion
}
