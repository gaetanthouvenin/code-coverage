// ------------------------------------------------------------------------------------------------
// <copyright file="ListCommitsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Commits.Queries.ListCommits;

/// <summary>Handler for the <see cref="ListCommitsQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="gitCommits">The cache of the commit metadata read from the forge.</param>
/// <param name="comparisons">The stored comparisons.</param>
/// <remarks>Database only: a commit whose metadata is not cached is listed without it, the forge is never asked.</remarks>
internal sealed class ListCommitsQueryHandler(
  CommitLocator locator,
  ICommitRepository commits,
  IGitCommitRepository gitCommits,
  IComparisonRepository comparisons) : IQueryHandler<ListCommitsQuery, CommitSummaryPage>
{
  #region Implementation of IQueryHandler<ListCommitsQuery,CommitSummaryPage>

  /// <inheritdoc />
  public async ValueTask<CommitSummaryPage> Handle(ListCommitsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var branch = string.IsNullOrEmpty(query.Branch) ? null : query.Branch;
    var page = await commits.ListPageAsync(repository.Id, branch, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);

    if (page.Items.Count == 0)
    {
      return new CommitSummaryPage([], page.Page, page.PageSize, page.TotalCount);
    }

    var metadata = (await gitCommits.ListByShasAsync(repository.Id, [.. page.Items.Select(static c => c.Sha)], cancellationToken).ConfigureAwait(false))
      .ToDictionary(static g => g.Sha);

    // The push comparison (no pull request) compares a commit with its parent on its branch: its delta is the one listed.
    var deltas = (await comparisons.ListByCommitsAsync([.. page.Items.Select(static c => c.Id)], cancellationToken).ConfigureAwait(false))
      .Where(static c => c.PullNumber is null)
      .ToDictionary(static c => c.CommitId, static c => c.Result.Delta);

    return new CommitSummaryPage(
      [.. page.Items.Select(c => new CommitSummary(c, metadata.GetValueOrDefault(c.Sha), deltas.GetValueOrDefault(c.Id)))],
      page.Page,
      page.PageSize,
      page.TotalCount
    );
  }

  #endregion
}
