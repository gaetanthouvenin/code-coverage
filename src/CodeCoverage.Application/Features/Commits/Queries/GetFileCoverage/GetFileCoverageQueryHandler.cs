// ------------------------------------------------------------------------------------------------
// <copyright file="GetFileCoverageQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Commits.Queries.GetFileCoverage;

/// <summary>Handler for the <see cref="GetFileCoverageQuery" /> query.</summary>
/// <param name="locator">Finds the commit of the address.</param>
/// <param name="reports">The stored reports of the commits.</param>
/// <param name="codec">The codec of the stored reports.</param>
/// <param name="flagView">Builds the report of one flag.</param>
internal sealed class GetFileCoverageQueryHandler(
  CommitLocator locator,
  ICommitReportRepository reports,
  ICoverageReportCodec codec,
  CommitFlagView flagView) : IQueryHandler<GetFileCoverageQuery, FileCoverageView>
{
  #region Implementation of IQueryHandler<GetFileCoverageQuery,FileCoverageView>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">The commit was compacted (<see cref="CommitErrors.Compacted" />).</exception>
  /// <exception cref="NotFoundException">The report of the commit has no such file (<see cref="CommitErrors.FileNotFound" />).</exception>
  public async ValueTask<FileCoverageView> Handle(GetFileCoverageQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var commit = await locator.FindCommitAsync(query.Owner, query.Name, query.Sha, cancellationToken).ConfigureAwait(false);
    var flag = string.IsNullOrEmpty(query.Flag) ? null : query.Flag;
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

    var path = query.Path.TrimStart('/');
    var file = report?.Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal));

    NotFoundException.ThrowIfCommitFileNotFound(file);

    return new FileCoverageView(file, flag);
  }

  #endregion
}
