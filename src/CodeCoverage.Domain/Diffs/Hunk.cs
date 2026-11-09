// ------------------------------------------------------------------------------------------------
// <copyright file="Hunk.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Diffs;

/// <summary>One block of changes of a unified diff.</summary>
/// <param name="OldStart">The first line of the block in the old file, as its header gives it.</param>
/// <param name="OldCount">The number of lines of the block in the old file.</param>
/// <param name="NewStart">The first line of the block in the new file, as its header gives it.</param>
/// <param name="NewCount">The number of lines of the block in the new file.</param>
/// <param name="RemovedLines">The removed lines, in ascending order, numbered in the old file.</param>
/// <param name="AddedLines">The added lines, in ascending order, numbered in the new file.</param>
public sealed record Hunk(
  int OldStart,
  int OldCount,
  int NewStart,
  int NewCount,
  IReadOnlyList<int> RemovedLines,
  IReadOnlyList<int> AddedLines);
