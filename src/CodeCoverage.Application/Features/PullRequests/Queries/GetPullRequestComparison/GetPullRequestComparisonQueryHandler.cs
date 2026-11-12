// ------------------------------------------------------------------------------------------------
// <copyright file="GetPullRequestComparisonQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Comparisons;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequestComparison;

/// <summary>Handler for the <see cref="GetPullRequestComparisonQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="pullRequests">The pull requests of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="comparisons">The comparisons stored for the commits.</param>
internal sealed class GetPullRequestComparisonQueryHandler(
  CommitLocator locator,
  IPullRequestRepository pullRequests,
  ICommitRepository commits,
  IComparisonRepository comparisons) : IQueryHandler<GetPullRequestComparisonQuery, ComparisonView>
{
  #region Implementation of IQueryHandler<GetPullRequestComparisonQuery,ComparisonView>

  /// <inheritdoc />
  public async ValueTask<ComparisonView> Handle(GetPullRequestComparisonQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var pullRequest = await pullRequests.FindByNumberAsync(repository.Id, query.Number, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfPullRequestNotFound(pullRequest);

    var head = await commits.FindByShaAsync(repository.Id, pullRequest.HeadSha, cancellationToken).ConfigureAwait(false);
    var comparison = head is null ? null : await comparisons.FindAsync(head.Id, pullRequest.Number, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfComparisonNotComputed(comparison);

    var comparedTo = comparison.ComparedToSha is { } baseSha
                       ? await commits.FindByShaAsync(repository.Id, baseSha, cancellationToken).ConfigureAwait(false)
                       : null;

    // A comparison is only found for a head that was found.
    return ComparisonView.Of(head!, comparison, comparedTo, pullRequest);
  }

  #endregion
}
