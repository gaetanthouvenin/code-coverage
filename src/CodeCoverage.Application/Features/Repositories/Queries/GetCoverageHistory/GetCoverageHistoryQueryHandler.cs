// ------------------------------------------------------------------------------------------------
// <copyright file="GetCoverageHistoryQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;

/// <summary>Handler for the <see cref="GetCoverageHistoryQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="reports">The totals of the flags of the commits.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   The totals are read by finalization date within the range, the most recent 10 000 at most; the points are then
///   dated by the date of their committer and grouped in C#, the same way on both database engines.
/// </remarks>
internal sealed class GetCoverageHistoryQueryHandler(CommitLocator locator, ICommitReportRepository reports, IClock clock)
  : IQueryHandler<GetCoverageHistoryQuery, IReadOnlyList<CoverageSeries>>
{
  #region Implementation of IQueryHandler<GetCoverageHistoryQuery,IReadOnlyList<CoverageSeries>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<CoverageSeries>> Handle(GetCoverageHistoryQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var branch = string.IsNullOrEmpty(query.Branch) ? repository.DefaultBranch : query.Branch;
    var to = query.To ?? clock.UtcNow;
    var from = query.From ?? to - GetCoverageHistoryQuery.DefaultSpan;
    var flags = query.Flags.Count == 0 ? [CommitFlagTotals.ProjectFlag] : query.Flags.Distinct(StringComparer.Ordinal).ToList();

    GetCoverageHistoryQuery.TryParseInterval(query.Interval, out var interval);

    var entries = await reports.ListHistoryAsync(repository.Id, branch, flags, from, to, GetCoverageHistoryQuery.MaxRows, cancellationToken)
                               .ConfigureAwait(false);

    return CoverageSeries.Build(entries, flags, interval, from, to);
  }

  #endregion
}
