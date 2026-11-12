// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Comparisons;

/// <summary>The error codes of the comparisons.</summary>
public static class ComparisonErrors
{
  #region Fields

  /// <summary>
  ///   No comparison of these commits is stored: the notification of the head computes it, never a read. Maps to HTTP
  ///   404 Not Found.
  /// </summary>
  public const string NotComputed = "Comparison.NotComputed";

  #endregion
}
