// ------------------------------------------------------------------------------------------------
// <copyright file="ListBranchesQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.ListBranches;

/// <summary>Handler for the <see cref="ListBranchesQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="branches">The branches of the organization.</param>
internal sealed class ListBranchesQueryHandler(CommitLocator locator, IBranchRepository branches) : IQueryHandler<ListBranchesQuery, IReadOnlyList<Branch>>
{
  #region Implementation of IQueryHandler<ListBranchesQuery,IReadOnlyList<Branch>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<Branch>> Handle(ListBranchesQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var all = await branches.ListAsync(repository.Id, cancellationToken).ConfigureAwait(false);

    return [.. all.OrderBy(static b => b.Name, StringComparer.Ordinal)];
  }

  #endregion
}
