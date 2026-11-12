// ------------------------------------------------------------------------------------------------
// <copyright file="CommitFlagView.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Builds the report of one flag of a commit from the reports of its sessions.</summary>
/// <param name="reader">Reads the sessions of a commit with their reports.</param>
/// <param name="views">Keeps the views of a report, by commit, revision, flags and paths.</param>
/// <remarks>
///   A session carrying the flag counts, joined or not (<see cref="ReportView.Build" />). The stored merged report and its
///   index hold every session, not one flag: the sessions have to be read.
/// </remarks>
internal sealed class CommitFlagView(CommitReportReader reader, IReportViewCache views)
{
  #region Fields

  private static readonly PathPatternSet EveryPath = new([]);

  #endregion

  #region Methods

  /// <summary>Builds the report of a flag.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="flag">The flag, case-sensitive.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The report of the sessions that carry the flag.</returns>
  /// <exception cref="UnprocessableEntityException">
  ///   The report of a session is no longer stored: the commit was compacted (<see cref="CommitErrors.Compacted" />).
  /// </exception>
  public async Task<CoverageReport> BuildAsync(Commit commit, string flag, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(flag);

    var sessions = await reader.ReadAsync(commit.Id, cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfCommitCompacted(sessions is null);

    var reports = sessions.Select(static s => new SessionReport(s.Session.Flags, s.Report)).ToList();

    return views.GetOrCreate(commit.Id, commit.ReportRevision, reports, [flag], EveryPath).Report;
  }

  #endregion
}
