// ------------------------------------------------------------------------------------------------
// <copyright file="SourceLineState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Source;

/// <summary>The coverage of a line of the annotated source.</summary>
public enum SourceLineState
{
  /// <summary>Not measured by the report.</summary>
  None,

  /// <summary>Hit.</summary>
  Hit,

  /// <summary>Missed.</summary>
  Miss,

  /// <summary>Partially hit: some branches missed.</summary>
  Partial,
}
