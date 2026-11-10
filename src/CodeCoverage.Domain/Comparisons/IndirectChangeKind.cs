// ------------------------------------------------------------------------------------------------
// <copyright file="IndirectChangeKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Comparisons;

/// <summary>How the coverage of a file changed without the file being in the diff.</summary>
public enum IndirectChangeKind
{
  /// <summary>Both reports hold the file, and the state of one line at least differs.</summary>
  Changed,

  /// <summary>Only the head report holds the file.</summary>
  Appeared,

  /// <summary>Only the base report holds the file.</summary>
  Disappeared,
}