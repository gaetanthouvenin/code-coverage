// ------------------------------------------------------------------------------------------------
// <copyright file="DiffLineKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>How a line of the <c>diff</c> table is drawn: the signs stay in its text, so the colour is never the only cue.</summary>
public enum DiffLineKind
{
  /// <summary>A separator or a row that did not move.</summary>
  Plain,

  /// <summary>The title and the row of the column titles.</summary>
  Head,

  /// <summary>A row that improved, starting with <c>+</c>.</summary>
  Add,

  /// <summary>A row that worsened, starting with <c>-</c>.</summary>
  Delete,
}
