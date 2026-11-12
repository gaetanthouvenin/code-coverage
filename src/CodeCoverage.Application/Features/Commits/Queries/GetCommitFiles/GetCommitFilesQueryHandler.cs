// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitFilesQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Commits.Queries.GetCommitFiles;

/// <summary>Handler for the <see cref="GetCommitFilesQuery" /> query.</summary>
/// <param name="locator">Finds the commit of the address.</param>
/// <param name="reports">The stored reports of the commits.</param>
/// <param name="codec">The codec of the stored reports.</param>
/// <param name="flagView">Builds the report of one flag.</param>
/// <remarks>
///   A commit not finalized yet has no index and lists no file. A finalized commit whose index is gone was compacted:
///   422 <c>Commit.Compacted</c>, as with a flag.
/// </remarks>
internal sealed class GetCommitFilesQueryHandler(
  CommitLocator locator,
  ICommitReportRepository reports,
  ICoverageReportCodec codec,
  CommitFlagView flagView) : IQueryHandler<GetCommitFilesQuery, IReadOnlyList<FileTotals>>
{
  #region Implementation of IQueryHandler<GetCommitFilesQuery,IReadOnlyList<FileTotals>>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">The commit was compacted.</exception>
  public async ValueTask<IReadOnlyList<FileTotals>> Handle(GetCommitFilesQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var commit = await locator.FindCommitAsync(query.Owner, query.Name, query.Sha, cancellationToken).ConfigureAwait(false);
    IReadOnlyList<FileTotals> files;

    if (!string.IsNullOrEmpty(query.Flag))
    {
      files = (await flagView.BuildAsync(commit, query.Flag, cancellationToken).ConfigureAwait(false)).FileTotals;
    }
    else if (await reports.GetFileIndexAsync(commit.Id, cancellationToken).ConfigureAwait(false) is { } index)
    {
      files = codec.DecodeFileIndex(index);
    }
    else
    {
      // A finalized commit without its index was compacted; one not finalized yet has no file.
      UnprocessableEntityException.ThrowIfCommitCompacted(commit.CompletedAt is not null);
      files = [];
    }

    var prefix = query.Path?.TrimStart('/');

    return
    [
      .. files.Where(f => string.IsNullOrEmpty(prefix) || f.Path.StartsWith(prefix, StringComparison.Ordinal))
              .OrderBy(static f => f.Path, StringComparer.Ordinal),
    ];
  }

  #endregion
}
