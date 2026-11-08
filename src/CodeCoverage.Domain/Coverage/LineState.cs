// ------------------------------------------------------------------------------------------------
// <copyright file="LineState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>How a coverable line counts in the totals.</summary>
public enum LineState
{
  /// <summary>The line is covered, or all its branches are.</summary>
  Hit,

  /// <summary>The line is not covered, or none of its branches is.</summary>
  Miss,

  /// <summary>Some of the branches of the line are covered, but not all.</summary>
  Partial,
}
