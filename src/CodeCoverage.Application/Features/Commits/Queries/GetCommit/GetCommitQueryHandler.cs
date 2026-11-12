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
/// <remarks>The sessions are read with their stored blob; only their metadata is used.</remarks>
internal sealed class GetCommitQueryHandler(
  CommitLocator locator,
  ICommitReportRepository reports,
  IUploadRepository uploads,
  IPostedStatusRepository statuses) : IQueryHandler<GetCommitQuery, CommitDetail>
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

    return new CommitDetail(commit, sessions, commitUploads, flags, posted);
  }

  #endregion
}
