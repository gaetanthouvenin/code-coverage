// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigValidationResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>What the validation of a <c>codecov.yml</c> file gives.</summary>
/// <param name="Normalized">
///   The content of the file once read, without any other layer, or <see langword="null" /> if it cannot be read.
/// </param>
/// <param name="Warnings">What could not be applied as written, each one once.</param>
/// <param name="Errors">Why the file cannot be read, if so.</param>
public sealed record ConfigValidationResult(
  ConfigNode? Normalized,
  IReadOnlyList<ConfigWarning> Warnings,
  IReadOnlyList<string> Errors)
{
  #region Properties

  /// <summary>Gets a value indicating whether the file can be used.</summary>
  public bool IsValid => Errors.Count == 0;

  #endregion
}
