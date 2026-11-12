// ------------------------------------------------------------------------------------------------
// <copyright file="ListPullRequestsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;

/// <summary>Handler for the <see cref="ListPullRequestsQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="pullRequests">The pull requests of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="comparisons">The comparisons stored for the commits.</param>
/// <remarks>Three reads whatever the size of the page: the pull requests, their head commits and their comparisons.</remarks>
internal sealed class ListPullRequestsQueryHandler(
  CommitLocator locator,
  IPullRequestRepository pullRequests,
  ICommitRepository commits,
  IComparisonRepository comparisons) : IQueryHandler<ListPullRequestsQuery, PullRequestSummaryPage>
{
  #region Implementation of IQueryHandler<ListPullRequestsQuery,PullRequestSummaryPage>

  /// <inheritdoc />
  public async ValueTask<PullRequestSummaryPage> Handle(ListPullRequestsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);

    ListPullRequestsQuery.TryParseState(query.State, out var state);

    var page = await pullRequests.ListPageAsync(repository.Id, state, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);
    var heads = await commits.ListByShasAsync(repository.Id, [.. page.Items.Select(static p => p.HeadSha).Distinct()], cancellationToken)
                             .ConfigureAwait(false);
    var stored = await comparisons.ListByCommitsAsync([.. heads.Select(static c => c.Id)], cancellationToken).ConfigureAwait(false);

    var items = page.Items
                    .Select(pull =>
                    {
                      var head = heads.FirstOrDefault(c => c.Sha == pull.HeadSha);
                      var comparison = head is null ? null : stored.FirstOrDefault(c => c.CommitId == head.Id && c.PullNumber == pull.Number);

                      return new PullRequestSummary(pull, comparison?.Result);
                    })
                    .ToList();

    return new PullRequestSummaryPage(items, page.Page, page.PageSize, page.TotalCount);
  }

  #endregion
}
