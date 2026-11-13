// ------------------------------------------------------------------------------------------------
// <copyright file="CommitFileReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Reads the coverage of one file of the report of a commit, or of one of its flags.</summary>
/// <remarks>The database and the report view cache only: nothing is asked of the forge.</remarks>
internal sealed class CommitFileReader(ICommitReportRepository reports, ICoverageReportCodec codec, CommitFlagView flagView)
{
  #region Methods

  /// <summary>Reads a file of the report of a commit.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="path">The path of the file; a leading <c>/</c> is ignored, the rest must equal a stored path (ordinal).</param>
  /// <param name="flag">The flag, or <see langword="null" /> for every session.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The coverage of the file.</returns>
  /// <exception cref="UnprocessableEntityException">The commit was compacted (<see cref="CommitErrors.Compacted" />).</exception>
  /// <exception cref="NotFoundException">The report of the commit has no such file (<see cref="CommitErrors.FileNotFound" />).</exception>
  public async Task<FileCoverage> ReadAsync(Commit commit, string path, string? flag, CancellationToken cancellationToken)
  {
    CoverageReport? report;

    if (flag is not null)
    {
      report = await flagView.BuildAsync(commit, flag, cancellationToken).ConfigureAwait(false);
    }
    else if (await reports.GetReportAsync(commit.Id, cancellationToken).ConfigureAwait(false) is { } blob)
    {
      report = codec.Decode(blob);
    }
    else
    {
      // A finalized commit without its report was compacted; one not finalized yet has no file.
      UnprocessableEntityException.ThrowIfCommitCompacted(commit.CompletedAt is not null);
      report = null;
    }

    var stored = path.TrimStart('/');
    var file = report?.Files.FirstOrDefault(f => string.Equals(f.Path, stored, StringComparison.Ordinal));

    NotFoundException.ThrowIfCommitFileNotFound(file);

    return file;
  }

  #endregion
}
