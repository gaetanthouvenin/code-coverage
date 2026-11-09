// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardBuilder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Commits;

/// <summary>Plans and builds the sessions that a commit inherits from an ancestor.</summary>
public static class CarryforwardBuilder
{
  #region Fields

  /// <summary>The name of a carried session that had none, as in Codecov.</summary>
  private const string UnnamedSession = "Carriedforward";

  /// <summary>The start of the name of a carried session.</summary>
  private const string CountPrefix = "CF[";

  /// <summary>What separates the number of copies from the original name.</summary>
  private const string CountSuffix = "] - ";

  #endregion

  #region Methods

  /// <summary>Plans what a finalization has to carry forward.</summary>
  /// <param name="carriedFlags">The flags with <c>carryforward: true</c>.</param>
  /// <param name="uploadedFlags">The flags of the sessions uploaded for the commit.</param>
  /// <returns>The plan.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static CarryforwardPlan Plan(IEnumerable<string> carriedFlags, IEnumerable<string> uploadedFlags)
  {
    ArgumentNullException.ThrowIfNull(carriedFlags);
    ArgumentNullException.ThrowIfNull(uploadedFlags);

    string[] uploaded = [.. uploadedFlags];

    return new CarryforwardPlan(carriedFlags.Except(uploaded, StringComparer.Ordinal), uploaded);
  }

  /// <summary>Moves a session of an ancestor onto the lines of the commit.</summary>
  /// <param name="plan">The plan of the finalization.</param>
  /// <param name="source">The session of the ancestor.</param>
  /// <param name="diff">The changes between the ancestor and the commit, or <see langword="null" /> when unavailable.</param>
  /// <param name="ancestorTree">The files of the ancestor, or <see langword="null" />.</param>
  /// <param name="commitTree">The files of the commit, or <see langword="null" />.</param>
  /// <param name="paths">The union of the <c>paths</c> of the concerned flags, applied to the paths of the ancestor.</param>
  /// <returns>The session, or <see langword="null" /> when the plan does not select it.</returns>
  /// <remarks>
  ///   A file whose lines cannot be followed, because its diff is unknown and its blob differs or cannot be
  ///   compared, is dropped: misaligned lines are never kept.
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="plan" />, <paramref name="source" /> or <paramref name="paths" /> is <see langword="null" />.
  /// </exception>
  public static CarriedSession? Build(
    CarryforwardPlan plan,
    CarryforwardSource source,
    DiffSet? diff,
    ForgeTree? ancestorTree,
    ForgeTree? commitTree,
    PathPatternSet paths)
  {
    ArgumentNullException.ThrowIfNull(plan);
    ArgumentNullException.ThrowIfNull(source);
    ArgumentNullException.ThrowIfNull(paths);

    if (!plan.Selects(source.Flags))
    {
      return null;
    }

    var changes = IndexByOldPath(diff);
    var ancestorBlobs = BlobsOf(ancestorTree);
    var commitBlobs = BlobsOf(commitTree);
    var isListComplete = diff is { Truncated: false };
    var files = new List<FileCoverage>();
    var dropped = new List<string>();
    var isIdentical = true;

    foreach (var file in source.Report.Files)
    {
      if (!paths.Includes(file.Path))
      {
        isIdentical = false;

        continue;
      }

      var change = changes.GetValueOrDefault(file.Path);
      var carried = Carry(file, change, isListComplete, ancestorBlobs, commitBlobs, dropped);

      isIdentical = isIdentical && carried is not null && IsSame(file, carried);

      if (carried is not null)
      {
        files.Add(carried);
      }
    }

    return new CarriedSession(
      source.SessionId,
      source.CarriedForwardFromSha ?? source.AncestorSha,
      isIdentical ? source.DataUploadId : null,
      RenameCopy(source.Name),
      CoverageReport.Create(files),
      dropped);
  }

  /// <summary>Gives the name of a session copied again, as <c>carriedforward_session_name</c> of Codecov does.</summary>
  /// <param name="name">The name of the source session, or <see langword="null" />.</param>
  /// <returns>The name of the copy.</returns>
  private static string RenameCopy(string? name)
  {
    if (string.IsNullOrEmpty(name))
    {
      return UnnamedSession;
    }

    var end = name.IndexOf(CountSuffix, StringComparison.Ordinal);

    if (name.StartsWith(CountPrefix, StringComparison.Ordinal)
        && end > CountPrefix.Length
        && int.TryParse(name.AsSpan(CountPrefix.Length, end - CountPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
        && count < int.MaxValue)
    {
      return string.Create(CultureInfo.InvariantCulture, $"{CountPrefix}{count + 1}{name[end..]}");
    }

    return $"{CountPrefix}1{CountSuffix}{name}";
  }

  /// <summary>Moves one file onto the lines of the commit.</summary>
  /// <param name="file">The file, in the ancestor.</param>
  /// <param name="change">Its change, or <see langword="null" /> when the diff does not list it.</param>
  /// <param name="isListComplete">A value indicating whether the diff lists every changed file.</param>
  /// <param name="ancestorBlobs">The blobs of the ancestor.</param>
  /// <param name="commitBlobs">The blobs of the commit.</param>
  /// <param name="dropped">Receives the files left out because their lines could not be followed.</param>
  /// <returns>The file in the commit, or <see langword="null" /> when it is left out.</returns>
  private static FileCoverage? Carry(
    FileCoverage file,
    FileDiff? change,
    bool isListComplete,
    Dictionary<string, GitSha> ancestorBlobs,
    Dictionary<string, GitSha> commitBlobs,
    List<string> dropped)
  {
    if (change is null && isListComplete)
    {
      return file;
    }

    if (change is { Status: FileDiffStatus.Removed })
    {
      return null;
    }

    if (change is { Status: FileDiffStatus.Unchanged })
    {
      return file;
    }

    if (change is { Status: FileDiffStatus.Modified or FileDiffStatus.Renamed, HasPatch: true })
    {
      return FileCoverage.Create(change.Path, LineShifter.Shift(change, file.Lines));
    }

    var isFollowable = change is null or { Status: FileDiffStatus.Modified or FileDiffStatus.Renamed };
    var newPath = change?.Path ?? file.Path;

    if (isFollowable
        && ancestorBlobs.TryGetValue(file.Path, out var ancestorBlob)
        && commitBlobs.TryGetValue(newPath, out var commitBlob)
        && ancestorBlob == commitBlob)
    {
      return string.Equals(newPath, file.Path, StringComparison.Ordinal) ? file : FileCoverage.Create(newPath, file.Lines);
    }

    dropped.Add(file.Path);

    return null;
  }

  /// <summary>Tells whether a carried file is exactly the file of the ancestor.</summary>
  /// <param name="file">The file of the ancestor.</param>
  /// <param name="carried">The carried file.</param>
  /// <returns><see langword="true" /> when its path and every line are the same.</returns>
  private static bool IsSame(FileCoverage file, FileCoverage carried)
  {
    return ReferenceEquals(file, carried)
      || (string.Equals(file.Path, carried.Path, StringComparison.Ordinal) && file.Lines.SequenceEqual(carried.Lines));
  }

  /// <summary>Indexes the changes by the path each file had in the ancestor.</summary>
  /// <param name="diff">The diff, or <see langword="null" />.</param>
  /// <returns>The changes; the first one wins when a forge lists a path twice.</returns>
  private static Dictionary<string, FileDiff> IndexByOldPath(DiffSet? diff)
  {
    var changes = new Dictionary<string, FileDiff>(StringComparer.Ordinal);

    foreach (var change in diff?.Files ?? [])
    {
      var oldPath = change.Status is FileDiffStatus.Renamed ? change.PreviousPath ?? change.Path : change.Path;

      changes.TryAdd(oldPath, change);
    }

    return changes;
  }

  /// <summary>Indexes the blobs of a tree by path.</summary>
  /// <param name="tree">The tree, or <see langword="null" />.</param>
  /// <returns>The SHA of the blob of each file.</returns>
  private static Dictionary<string, GitSha> BlobsOf(ForgeTree? tree)
  {
    var blobs = new Dictionary<string, GitSha>(StringComparer.Ordinal);

    foreach (var entry in tree?.Entries ?? [])
    {
      if (!entry.IsDirectory)
      {
        blobs.TryAdd(entry.Path, entry.Sha);
      }
    }

    return blobs;
  }

  #endregion
}
