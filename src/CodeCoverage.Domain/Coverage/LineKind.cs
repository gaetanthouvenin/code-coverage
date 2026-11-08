// ------------------------------------------------------------------------------------------------
// <copyright file="LineKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>What a coverable line is, as far as the report tells.</summary>
public enum LineKind
{
  /// <summary>A plain line.</summary>
  Line,

  /// <summary>A line with branch data.</summary>
  Branch,

  /// <summary>A line that declares a method.</summary>
  Method,
}
