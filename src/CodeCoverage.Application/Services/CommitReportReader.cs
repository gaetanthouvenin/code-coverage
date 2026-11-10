// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Reads the sessions of a finalized commit with the report of each one.</summary>
/// <param name="reports">The repository of the reports.</param>
/// <param name="codec">The codec of the stored reports.</param>
internal sealed class CommitReportReader(ICommitReportRepository reports, ICoverageReportCodec codec)
{
  #region Methods

  /// <summary>Reads the sessions of a commit.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The sessions, by number, or <see langword="null" /> when the report of one of them is no longer
  ///   stored: a part of a commit is never read as the whole of it.
  /// </returns>
  public async Task<IReadOnlyList<SessionCoverage>?> ReadAsync(long commitId, CancellationToken cancellationToken)
  {
    var sessions = await reports.ListSessionsAsync(commitId, cancellationToken).ConfigureAwait(false);
    var read = new List<SessionCoverage>(sessions.Count);

    foreach (var session in sessions.OrderBy(static s => s.Number))
    {
      if (await ReadReportAsync(session, cancellationToken).ConfigureAwait(false) is not { } report)
      {
        return null;
      }

      read.Add(new SessionCoverage(session, report));
    }

    return read;
  }

  /// <summary>Reads the report of a session: its own blob when its lines were shifted, else the one of its upload.</summary>
  /// <param name="session">The session.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The report, or <see langword="null" /> when it is no longer stored.</returns>
  private async Task<CoverageReport?> ReadReportAsync(Session session, CancellationToken cancellationToken)
  {
    if (session.Data is { } data)
    {
      return codec.Decode(data);
    }

    if ((session.DataUploadId ?? session.UploadId) is not { } uploadId)
    {
      return null;
    }

    var blob = await reports.GetUploadCoverageAsync(uploadId, cancellationToken).ConfigureAwait(false);

    return blob is null ? null : codec.Decode(blob);
  }

  #endregion
}