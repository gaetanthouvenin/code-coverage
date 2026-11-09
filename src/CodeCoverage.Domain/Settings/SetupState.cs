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
public sealed record SetupState(bool GitHubAppCreated, bool PublicUrlsConfigured, bool AdministratorsConfigured)
{
  #region Properties

  /// <summary>Gets a value indicating whether nothing is left to do.</summary>
  public bool IsComplete => GitHubAppCreated && PublicUrlsConfigured && AdministratorsConfigured;

  #endregion
}
