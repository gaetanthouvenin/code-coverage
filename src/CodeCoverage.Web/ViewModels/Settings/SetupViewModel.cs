// ------------------------------------------------------------------------------------------------
// <copyright file="SetupViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>What the first-start wizard has done on this instance.</summary>
/// <param name="IsAppCreated">Whether the GitHub App is known, created here or given by the environment.</param>
/// <param name="HasPublicUrls">Whether both public addresses are known.</param>
/// <param name="HasAdministrators">Whether at least one administrator of the instance is named.</param>
/// <param name="WebPublicUrl">The public address of the console, or <see langword="null" /> when it is not set.</param>
/// <param name="ApiPublicUrl">The public address of the Api, or <see langword="null" /> when it is not set.</param>
public sealed record SetupViewModel(
  bool IsAppCreated,
  bool HasPublicUrls,
  bool HasAdministrators,
  string? WebPublicUrl = null,
  string? ApiPublicUrl = null)
{
  #region Properties

  /// <summary>Gets a value indicating whether the setup is over.</summary>
  public bool IsComplete => IsAppCreated && HasPublicUrls && HasAdministrators;

  #endregion
}
