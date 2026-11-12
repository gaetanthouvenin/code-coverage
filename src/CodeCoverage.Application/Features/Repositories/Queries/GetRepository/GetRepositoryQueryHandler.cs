// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositoryQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetRepository;

/// <summary>Handler for the <see cref="GetRepositoryQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="branches">The branches of the organization.</param>
/// <param name="flags">The flags of the repositories.</param>
/// <param name="reports">The totals of the flags of the commits.</param>
/// <remarks>The last totals are those of the covered head of the default branch (<c>Branches.CoveredCommitId</c>, §11.1).</remarks>
internal sealed class GetRepositoryQueryHandler(
  CommitLocator locator,
  IBranchRepository branches,
  IRepositoryFlagRepository flags,
  ICommitReportRepository reports) : IQueryHandler<GetRepositoryQuery, RepositoryOverview>
{
  #region Implementation of IQueryHandler<GetRepositoryQuery,RepositoryOverview>

  /// <inheritdoc />
  public async ValueTask<RepositoryOverview> Handle(GetRepositoryQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var all = await branches.ListAsync(repository.Id, cancellationToken).ConfigureAwait(false);
    var repositoryFlags = await flags.ListAsync(repository.Id, cancellationToken).ConfigureAwait(false);
    var head = all.FirstOrDefault(b => string.Equals(b.Name, repository.DefaultBranch, StringComparison.Ordinal))?.CoveredCommitId;
    var totals = head is { } commitId ? await reports.ListFlagTotalsAsync(commitId, cancellationToken).ConfigureAwait(false) : [];

    return new RepositoryOverview(repository, [.. all.OrderBy(static b => b.Name, StringComparer.Ordinal)], [.. repositoryFlags.Order(StringComparer.Ordinal)], totals);
  }

  #endregion
}
