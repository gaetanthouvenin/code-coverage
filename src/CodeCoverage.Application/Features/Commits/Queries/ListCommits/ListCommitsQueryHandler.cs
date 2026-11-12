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
internal sealed class ListCommitsQueryHandler(CommitLocator locator, ICommitRepository commits) : IQueryHandler<ListCommitsQuery, CommitPage>
{
  #region Implementation of IQueryHandler<ListCommitsQuery,CommitPage>

  /// <inheritdoc />
  public async ValueTask<CommitPage> Handle(ListCommitsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var branch = string.IsNullOrEmpty(query.Branch) ? null : query.Branch;

    return await commits.ListPageAsync(repository.Id, branch, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
