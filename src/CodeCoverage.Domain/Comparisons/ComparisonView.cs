// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonView.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>A comparison stored for a commit, as a read of the API or of a page shows it.</summary>
/// <param name="HeadSha">The SHA of the head commit.</param>
/// <param name="Comparison">The stored comparison.</param>
/// <param name="PullRequest">The pull request it was computed for, or <see langword="null" /> for a push build.</param>
/// <param name="IsStale">
///   Whether the report of the head, or of the base, changed since the comparison was computed: the next notification
///   computes it again.
/// </param>
/// <param name="HeadFiles">The number of files of the head report, read in its row.</param>
/// <param name="BaseFiles">The number of files of the base report, or <see langword="null" /> without a base report.</param>
/// <remarks>A read model: nothing is computed when it is read.</remarks>
public sealed record ComparisonView(GitSha HeadSha, CommitComparison Comparison, PullRequest? PullRequest, bool IsStale, int HeadFiles = 0, int? BaseFiles = null)
{
  #region Methods

  /// <summary>Builds the view of a comparison stored for a commit.</summary>
  /// <param name="head">The head commit.</param>
  /// <param name="comparison">The comparison stored for it.</param>
  /// <param name="comparedTo">The commit it was compared to, or <see langword="null" /> when it has no base or the base is gone.</param>
  /// <param name="pullRequest">The pull request, or <see langword="null" /> for a push build.</param>
  /// <returns>The view; it is stale when the revision of the report of the head or of the base moved since.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="head" /> or <paramref name="comparison" /> is <see langword="null" />.</exception>
  public static ComparisonView Of(Commit head, CommitComparison comparison, Commit? comparedTo, PullRequest? pullRequest)
  {
    ArgumentNullException.ThrowIfNull(head);
    ArgumentNullException.ThrowIfNull(comparison);

    var headMoved = comparison.HeadRevision != head.ReportRevision;
    var baseMoved = comparison.ComparedToSha is not null && comparison.BaseRevision != comparedTo?.ReportRevision;

    // CoverageTotals counts no file: the rows of the commits do, the base's only when its report was read.
    var baseFiles = comparison.Result.Base is null ? null : comparedTo?.Files;

    return new ComparisonView(head.Sha, comparison, pullRequest, headMoved || baseMoved, head.Files, baseFiles);
  }

  #endregion
}
