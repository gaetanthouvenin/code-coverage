// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Infrastructure.Comparisons;

/// <summary>A row of the <c>Comparisons</c> table: the comparison computed for a commit.</summary>
/// <remarks>
///   The columns hold what a list or a later notification reads without decoding <see cref="Data" />, which
///   holds the whole comparison (<see cref="ComparisonDataSerializer" />).
/// </remarks>
internal sealed class ComparisonRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the row.</summary>
  public long Id { get; set; }

  /// <summary>Gets or sets the identifier of the head commit.</summary>
  public long CommitId { get; set; }

  /// <summary>Gets or sets the number of the pull request, or 0 for the comparison of a push build.</summary>
  /// <remarks>Never null: the two engines do not agree on whether two nulls are equal in a unique index.</remarks>
  public int PullNumber { get; set; }

  /// <summary>Gets or sets the fork point of the pull request, or <see langword="null" />.</summary>
  public GitSha? ForkPointSha { get; set; }

  /// <summary>Gets or sets the commit whose report is the base, or <see langword="null" />.</summary>
  public GitSha? ComparedToSha { get; set; }

  /// <summary>Gets or sets the revision of the head report.</summary>
  public int HeadRevision { get; set; }

  /// <summary>Gets or sets the revision of the base report, or <see langword="null" />.</summary>
  public int? BaseRevision { get; set; }

  /// <summary>Gets or sets the coverage of the head, or <see langword="null" />.</summary>
  public decimal? HeadCoverage { get; set; }

  /// <summary>Gets or sets the coverage of the base, or <see langword="null" />.</summary>
  public decimal? BaseCoverage { get; set; }

  /// <summary>Gets or sets the number of lines of the patch.</summary>
  public int PatchLines { get; set; }

  /// <summary>Gets or sets the number of lines of the patch that are hit.</summary>
  public int PatchHits { get; set; }

  /// <summary>Gets or sets the number of lines of the patch that are missed.</summary>
  public int PatchMisses { get; set; }

  /// <summary>Gets or sets the number of lines of the patch that are partially hit.</summary>
  public int PatchPartials { get; set; }

  /// <summary>Gets or sets the number of indirect changes.</summary>
  public int IndirectChangeCount { get; set; }

  /// <summary>Gets or sets where the patch diff came from.</summary>
  public DiffSource DiffSource { get; set; }

  /// <summary>Gets or sets a value indicating whether a diff of the comparison lacks files.</summary>
  public bool DiffIncomplete { get; set; }

  /// <summary>Gets or sets the whole comparison, serialized.</summary>
  public byte[] Data { get; set; } = [];

  /// <summary>Gets or sets when the comparison was computed, in UTC.</summary>
  public DateTimeOffset ComputedAt { get; set; }

  #endregion
}