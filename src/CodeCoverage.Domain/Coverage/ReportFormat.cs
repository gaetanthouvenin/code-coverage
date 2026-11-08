// ------------------------------------------------------------------------------------------------
// <copyright file="ReportFormat.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The format of a coverage report.</summary>
public enum ReportFormat
{
  /// <summary>Cobertura XML.</summary>
  Cobertura,

  /// <summary>OpenCover XML.</summary>
  OpenCover,

  /// <summary>LCOV tracefile.</summary>
  Lcov,

  /// <summary>JaCoCo XML.</summary>
  Jacoco,

  /// <summary>Clover XML.</summary>
  Clover,
}
