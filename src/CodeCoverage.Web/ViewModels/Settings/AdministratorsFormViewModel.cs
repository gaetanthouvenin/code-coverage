// ------------------------------------------------------------------------------------------------
// <copyright file="AdministratorsFormViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>The administrators of the instance, as the wizard asks for them.</summary>
/// <remarks>The name of the property is the one of the command, so a refusal shows under the field.</remarks>
public sealed class AdministratorsFormViewModel
{
  #region Properties

  /// <summary>Gets or sets the numeric identifiers of the administrators, separated by commas.</summary>
  public string AdminUserIds { get; set; } = string.Empty;

  #endregion
}
