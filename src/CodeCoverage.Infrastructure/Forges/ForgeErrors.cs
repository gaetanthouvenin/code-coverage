// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>The error codes shared by the adapters of every forge.</summary>
/// <remarks>An adapter reuses these codes instead of creating its own for the same refusal.</remarks>
public static class ForgeErrors
{
  #region Fields

  /// <summary>
  ///   The forge refused a commit status for good: it does not know the repository or the commit, the application may
  ///   not write statuses there, or the commit holds as many statuses as the forge allows.
  /// </summary>
  public const string StatusRefused = "Forge.StatusRefused";

  /// <summary>The forge failed for good: its API answers with an error, or does not answer.</summary>
  public const string Unavailable = "Forge.Unavailable";

  #endregion
}
