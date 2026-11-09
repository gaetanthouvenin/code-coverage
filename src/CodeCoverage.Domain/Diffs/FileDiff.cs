// ------------------------------------------------------------------------------------------------
// <copyright file="FileDiff.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Diffs;

/// <summary>The changes of one file between two commits.</summary>
public sealed class FileDiff
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="FileDiff" /> class.</summary>
  /// <param name="path">The path of the file in the new commit.</param>
  /// <param name="previousPath">The path it had before a renaming, or <see langword="null" />.</param>
  /// <param name="status">How the file changed.</param>
  /// <param name="patch">The unified diff of the file, or <see langword="null" />.</param>
  /// <param name="hunks">The blocks of changes of the patch.</param>
  private FileDiff(
    string path,
    string? previousPath,
    FileDiffStatus status,
    string? patch,
    IReadOnlyList<Hunk> hunks)
  {
    Path = path;
    PreviousPath = previousPath;
    Status = status;
    Patch = patch;
    Hunks = hunks;
    RemovedLines = [.. hunks.SelectMany(hunk => hunk.RemovedLines)];
    AddedLines = [.. hunks.SelectMany(hunk => hunk.AddedLines)];
  }

  #endregion

  #region Properties

  /// <summary>Gets the path of the file in the new commit.</summary>
  public string Path { get; }

  /// <summary>Gets the path the file had before a renaming, or <see langword="null" />.</summary>
  public string? PreviousPath { get; }

  /// <summary>Gets how the file changed.</summary>
  public FileDiffStatus Status { get; }

  /// <summary>
  ///   Gets the unified diff of the file, or <see langword="null" /> when no usable one is known: an
  ///   unknown patch is never an empty one.
  /// </summary>
  public string? Patch { get; }

  /// <summary>Gets a value indicating whether the lines of the file can be followed through the diff.</summary>
  public bool HasPatch => Patch is not null;

  /// <summary>Gets the blocks of changes; none without a patch.</summary>
  public IReadOnlyList<Hunk> Hunks { get; }

  /// <summary>Gets the removed lines of all the blocks, in ascending order, numbered in the old file.</summary>
  public IReadOnlyList<int> RemovedLines { get; }

  /// <summary>Gets the added lines of all the blocks, in ascending order, numbered in the new file.</summary>
  public IReadOnlyList<int> AddedLines { get; }

  #endregion

  #region Methods

  /// <summary>Creates the changes of a file.</summary>
  /// <param name="path">The path of the file in the new commit.</param>
  /// <param name="previousPath">The path it had before a renaming, or <see langword="null" />.</param>
  /// <param name="status">How the file changed.</param>
  /// <param name="patch">The unified diff of the file, or <see langword="null" /> when it is not known.</param>
  /// <returns>The changes; a patch that cannot be read counts as unknown, so no line number is guessed.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="path" /> is blank.</exception>
  public static FileDiff Create(string path, string? previousPath, FileDiffStatus status, string? patch)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    if (patch is null)
    {
      return new FileDiff(path, previousPath, status, null, []);
    }

    try
    {
      return new FileDiff(path, previousPath, status, patch, UnifiedDiffParser.Parse(patch));
    }
    catch (FormatException)
    {
      return new FileDiff(path, previousPath, status, null, []);
    }
  }

  #endregion
}
