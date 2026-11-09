// ------------------------------------------------------------------------------------------------
// <copyright file="DiffSet.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Diffs;

/// <summary>The files changed between two commits, as GitHub returned them.</summary>
/// <param name="Files">The changed files that GitHub returned.</param>
/// <param name="Truncated">A value indicating whether GitHub left out some of the changed files.</param>
/// <param name="Source">The API the files come from.</param>
/// <remarks>
///   A file listed without its patch does not make the set truncated: <see cref="FileDiff.HasPatch" />
///   tells it for each file.
/// </remarks>
public sealed record DiffSet(IReadOnlyList<FileDiff> Files, bool Truncated, DiffSource Source)
{
  #region Properties

  /// <summary>Gets a value indicating whether GitHub returned every changed file.</summary>
  public bool Complete => !Truncated;

  #endregion
}
