// ------------------------------------------------------------------------------------------------
// <copyright file="CompareCommitsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Comparisons.Queries.CompareCommits;

/// <summary>Handler for the <see cref="CompareCommitsQuery" /> query.</summary>
/// <param name="locator">Finds the head commit of the address.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="pullRequests">The pull requests of the organization.</param>
/// <param name="comparisons">The comparisons stored for the commits.</param>
internal sealed class CompareCommitsQueryHandler(
  CommitLocator locator,
  ICommitRepository commits,
  IPullRequestRepository pullRequests,
  IComparisonRepository comparisons) : IQueryHandler<CompareCommitsQuery, ComparisonView>
{
  #region Implementation of IQueryHandler<CompareCommitsQuery,ComparisonView>

  /// <inheritdoc />
  public async ValueTask<ComparisonView> Handle(CompareCommitsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var head = await locator.FindCommitAsync(query.Owner, query.Name, query.Head, cancellationToken).ConfigureAwait(false);
    var comparedTo = await commits.FindByShaAsync(head.RepositoryId, query.Base, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfCommitNotFound(comparedTo);

    var stored = await comparisons.ListByCommitsAsync([head.Id], cancellationToken).ConfigureAwait(false);
    var comparison = stored.Where(c => c.ComparedToSha == query.Base)
                           .OrderBy(static c => c.PullNumber is null ? 0 : 1)
                           .ThenByDescending(static c => c.ComputedAt)
                           .FirstOrDefault();

    NotFoundException.ThrowIfComparisonNotComputed(comparison);

    var pullRequest = comparison.PullNumber is { } number
                        ? await pullRequests.FindByNumberAsync(head.RepositoryId, number, cancellationToken).ConfigureAwait(false)
                        : null;

    return ComparisonView.Of(head, comparison, comparedTo, pullRequest);
  }

  #endregion
}
