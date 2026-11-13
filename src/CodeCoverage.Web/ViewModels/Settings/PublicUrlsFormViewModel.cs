// ------------------------------------------------------------------------------------------------
// <copyright file="PublicUrlsFormViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>The public addresses of the hosts, as the wizard asks for them.</summary>
/// <remarks>The names of the properties are those of the command, so a refusal shows under the right field.</remarks>
public sealed class PublicUrlsFormViewModel
{
  #region Properties

  /// <summary>Gets or sets the public address of the console.</summary>
  public string WebPublicUrl { get; set; } = string.Empty;

  /// <summary>Gets or sets the public address of the Api.</summary>
  public string ApiPublicUrl { get; set; } = string.Empty;

  #endregion
}
