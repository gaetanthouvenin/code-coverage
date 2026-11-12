// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Commits.Queries.GetCommit;

/// <summary>Handler for the <see cref="GetCommitQuery" /> query.</summary>
/// <param name="locator">Finds the commit of the address.</param>
/// <param name="reports">The sessions and the totals of the flags of the commits.</param>
/// <param name="uploads">The uploads of the organization.</param>
/// <param name="statuses">The statuses posted for the commits.</param>
/// <param name="gitCommits">The cache of the commit metadata read from the forge.</param>
/// <param name="configs">The configurations stored for the commits.</param>
/// <param name="comparisons">The stored comparisons.</param>
/// <param name="commits">The commits, for the files of the base of the push comparison.</param>
/// <remarks>
///   The sessions are read with their stored blob; only their metadata is used. Database only: the forge is never asked
///   and the configuration is not resolved again.
/// </remarks>
internal sealed class GetCommitQueryHandler(
  CommitLocator locator,
  ICommitReportRepository reports,
  IUploadRepository uploads,
  IPostedStatusRepository statuses,
  IGitCommitRepository gitCommits,
  ICommitConfigRepository configs,
  IComparisonRepository comparisons,
  ICommitRepository commits) : IQueryHandler<GetCommitQuery, CommitDetail>
{
  #region Implementation of IQueryHandler<GetCommitQuery,CommitDetail>

  /// <inheritdoc />
  public async ValueTask<CommitDetail> Handle(GetCommitQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var commit = await locator.FindCommitAsync(query.Owner, query.Name, query.Sha, cancellationToken).ConfigureAwait(false);
    var sessions = await reports.ListSessionsAsync(commit.Id, cancellationToken).ConfigureAwait(false);
    var commitUploads = await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);
    var flags = await reports.ListFlagTotalsAsync(commit.Id, cancellationToken).ConfigureAwait(false);
    var posted = await statuses.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);

    var git = await gitCommits.FindAsync(commit.RepositoryId, commit.Sha, cancellationToken).ConfigureAwait(false);
    var config = await configs.FindAsync(commit.RepositoryId, commit.Sha, cancellationToken).ConfigureAwait(false);

    // The push comparison (no pull request): the commit against its parent on its branch.
    var comparison = await comparisons.FindAsync(commit.Id, null, cancellationToken).ConfigureAwait(false);

    var @base = comparison?.ComparedToSha is { } baseSha ? await commits.FindByShaAsync(commit.RepositoryId, baseSha, cancellationToken).ConfigureAwait(false) : null;

    return new CommitDetail(commit, sessions, commitUploads, flags, posted, git, config, comparison, @base?.Files);
  }

  #endregion
}
