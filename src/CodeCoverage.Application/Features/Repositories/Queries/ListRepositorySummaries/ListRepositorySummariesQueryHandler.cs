// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositorySummariesQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.ListRepositorySummaries;

/// <summary>Handler for the <see cref="ListRepositorySummariesQuery" /> query.</summary>
/// <param name="currentUser">The user of the request.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="access">The repositories the user can read.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="flags">The flags known to the repositories.</param>
/// <param name="reports">The totals of the commits.</param>
/// <param name="clock">The clock.</param>
/// <remarks>The readable filter is applied before anything else is read: no total of an unreadable repository is loaded.</remarks>
internal sealed class ListRepositorySummariesQueryHandler(
  ICurrentUser currentUser,
  IRepositoryRepository repositories,
  IRepositoryAccessService access,
  ICommitRepository commits,
  IRepositoryFlagRepository flags,
  ICommitReportRepository reports,
  IClock clock) : IQueryHandler<ListRepositorySummariesQuery, IReadOnlyList<RepositorySummary>>
{
  #region Implementation of IQueryHandler<ListRepositorySummariesQuery,IReadOnlyList<RepositorySummary>>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">No user is signed in.</exception>
  public async ValueTask<IReadOnlyList<RepositorySummary>> Handle(ListRepositorySummariesQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var userId = currentUser.UserId;
    UnauthorizedException.ThrowIfNoUser(userId);

    var readable = await access.ListReadableAsync(userId.Value, cancellationToken).ConfigureAwait(false);
    var active = await repositories.SearchActiveAsync(query.Search, cancellationToken).ConfigureAwait(false);
    List<TrackedRepository> listed = [.. active.Where(r => readable.Contains(r.ExternalId) && (query.Active is not { } wanted || (r.DisabledAt is null) == wanted))];

    if (listed.Count == 0)
    {
      return [];
    }

    long[] ids = [.. listed.Select(static r => r.Id)];
    var now = clock.UtcNow;
    var heads = (await commits.ListCoveredDefaultHeadsAsync(ids, cancellationToken).ConfigureAwait(false)).ToDictionary(static c => c.RepositoryId);
    var known = await flags.ListByRepositoriesAsync(ids, cancellationToken).ConfigureAwait(false);
    var totals = await reports.ListDefaultBranchHistoryAsync(ids, now.AddDays(-ListRepositorySummariesQuery.HistoryDays), now, ListRepositorySummariesQuery.MaxHistoryRows, cancellationToken)
                              .ConfigureAwait(false);

    // One point a day: the coverage of the last commit finalized that day.
    var history = totals.GroupBy(static t => t.RepositoryId)
                        .ToDictionary(
                          static g => g.Key,
                          static g => (IReadOnlyList<DailyCoverage>)
                          [
                            .. g.OrderBy(static t => t.CompletedAt)
                                .ThenBy(static t => t.CommitId)
                                .GroupBy(static t => CoverageSeries.PeriodOf(t.CompletedAt, HistoryInterval.Day))
                                .Select(static d => new DailyCoverage(d.Key, d.Last().Coverage)),
                          ]);

    return
    [
      .. listed.Select(r => new RepositorySummary(r, heads.GetValueOrDefault(r.Id), known.GetValueOrDefault(r.Id) ?? [], history.GetValueOrDefault(r.Id) ?? [])),
    ];
  }

  #endregion
}
