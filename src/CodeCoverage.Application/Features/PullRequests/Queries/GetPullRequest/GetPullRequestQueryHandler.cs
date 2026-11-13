// ------------------------------------------------------------------------------------------------
// <copyright file="GetPullRequestQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.PullRequests;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequest;

/// <summary>Handler for the <see cref="GetPullRequestQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="pullRequests">The pull requests of the repositories.</param>
/// <param name="commits">The commits of the repositories.</param>
/// <param name="comparisons">The stored comparisons.</param>
internal sealed class GetPullRequestQueryHandler(
  CommitLocator locator,
  IPullRequestRepository pullRequests,
  ICommitRepository commits,
  IComparisonRepository comparisons) : IQueryHandler<GetPullRequestQuery, PullRequestDetail>
{
  #region Implementation of IQueryHandler<GetPullRequestQuery,PullRequestDetail>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">
  ///   The repository is not readable (<c>Repository.NotFound</c>) or has no such pull request
  ///   (<see cref="PullRequestErrors.NotFound" />).
  /// </exception>
  public async ValueTask<PullRequestDetail> Handle(GetPullRequestQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var pullRequest = await pullRequests.FindByNumberAsync(repository.Id, query.Number, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfPullRequestNotFound(pullRequest);

    // A head with no upload yet, or not compared yet: the pull request is shown, its comparison is pending.
    var head = await commits.FindByShaAsync(repository.Id, pullRequest.HeadSha, cancellationToken).ConfigureAwait(false);
    var comparison = head is null ? null : await comparisons.FindAsync(head.Id, pullRequest.Number, cancellationToken).ConfigureAwait(false);

    if (head is null || comparison is null)
    {
      return new PullRequestDetail(pullRequest, null);
    }

    var comparedTo = comparison.ComparedToSha is { } baseSha
                       ? await commits.FindByShaAsync(repository.Id, baseSha, cancellationToken).ConfigureAwait(false)
                       : null;

    return new PullRequestDetail(pullRequest, ComparisonView.Of(head, comparison, comparedTo, pullRequest));
  }

  #endregion
}
