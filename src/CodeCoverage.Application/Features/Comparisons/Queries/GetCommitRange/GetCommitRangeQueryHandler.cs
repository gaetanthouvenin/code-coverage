// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitRangeQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Comparisons.Queries.GetCommitRange;

/// <summary>Handler for the <see cref="GetCommitRangeQuery" /> query.</summary>
/// <param name="locator">Finds the head commit of the address.</param>
/// <param name="commits">The commits of the repositories.</param>
/// <param name="comparisons">The stored comparisons.</param>
/// <param name="indirectTotals">Reads the head totals of the files of the indirect changes.</param>
internal sealed class GetCommitRangeQueryHandler(
  CommitLocator locator,
  ICommitRepository commits,
  IComparisonRepository comparisons,
  IndirectHeadTotalsReader indirectTotals) : IQueryHandler<GetCommitRangeQuery, CommitRange>
{
  #region Implementation of IQueryHandler<GetCommitRangeQuery,CommitRange>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not readable, or one of the commits is unknown (<see cref="CommitErrors.NotFound" />).</exception>
  public async ValueTask<CommitRange> Handle(GetCommitRangeQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var head = await locator.FindCommitAsync(query.Owner, query.Name, query.Head, cancellationToken).ConfigureAwait(false);
    var comparedTo = await commits.FindByShaAsync(head.RepositoryId, query.Base, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfCommitNotFound(comparedTo);

    // The comparison of the push build first, as CompareCommits serves it; the pull request is not shown here.
    var stored = await comparisons.ListByCommitsAsync([head.Id], cancellationToken).ConfigureAwait(false);
    var comparison = stored.Where(c => c.ComparedToSha == query.Base)
                           .OrderBy(static c => c.PullNumber is null ? 0 : 1)
                           .ThenByDescending(static c => c.ComputedAt)
                           .FirstOrDefault();

    if (comparison is null)
    {
      return new CommitRange(query.Base, query.Head, null);
    }

    var totals = await indirectTotals.ReadAsync(head.Id, comparison.Result, cancellationToken).ConfigureAwait(false);

    return new CommitRange(query.Base, query.Head, ComparisonView.Of(head, comparison, comparedTo, null), totals);
  }

  #endregion
}
