// ------------------------------------------------------------------------------------------------
// <copyright file="HistoryEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.History;

/// <summary>The totals of a flag of a finalized commit, as the history reads them.</summary>
/// <param name="CommitId">The identifier of the commit.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Flag">The flag, case-sensitive; the empty text for the whole project.</param>
/// <param name="CompletedAt">When the commit was finalized, in UTC.</param>
/// <param name="CommittedAt">The date of its committer on the forge, or <see langword="null" /> when it was never read.</param>
/// <param name="Lines">The number of coverable lines.</param>
/// <param name="Hits">The number of covered lines.</param>
/// <param name="Misses">The number of lines that are not covered.</param>
/// <param name="Partials">The number of partially covered lines.</param>
/// <param name="Coverage">The coverage in percent, or <see langword="null" /> without a coverable line.</param>
/// <remarks>A read model.</remarks>
public sealed record HistoryEntry(
  long CommitId,
  GitSha Sha,
  string Flag,
  DateTimeOffset CompletedAt,
  DateTimeOffset? CommittedAt,
  int Lines,
  int Hits,
  int Misses,
  int Partials,
  decimal? Coverage)
{
  #region Properties

  /// <summary>Gets the date of the point: the date of the committer, else the finalization, in UTC.</summary>
  public DateTimeOffset At => (CommittedAt ?? CompletedAt).ToUniversalTime();

  #endregion
}
