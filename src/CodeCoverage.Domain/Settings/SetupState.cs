// ------------------------------------------------------------------------------------------------
// <copyright file="SetupState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>What the first-start wizard has done on this instance.</summary>
/// <param name="GitHubAppCreated">Whether the GitHub App is known, created here or provided by the environment.</param>
/// <param name="PublicUrlsConfigured">Whether both public addresses are known.</param>
/// <param name="AdministratorsConfigured">Whether at least one administrator of the instance is named.</param>
/// <param name="WebPublicUrl">The public address of the console, or <see langword="null" /> when it is not set.</param>
/// <param name="ApiPublicUrl">The public address of the Api, or <see langword="null" /> when it is not set.</param>
/// <remarks>The addresses are public by nature, and the wizard needs them again to write the manifest of the App.</remarks>
public sealed record SetupState(
  bool GitHubAppCreated,
  bool PublicUrlsConfigured,
  bool AdministratorsConfigured,
  string? WebPublicUrl = null,
  string? ApiPublicUrl = null)
{
  #region Properties

  /// <summary>Gets a value indicating whether nothing is left to do.</summary>
  public bool IsComplete => GitHubAppCreated && PublicUrlsConfigured && AdministratorsConfigured;

  #endregion
}
