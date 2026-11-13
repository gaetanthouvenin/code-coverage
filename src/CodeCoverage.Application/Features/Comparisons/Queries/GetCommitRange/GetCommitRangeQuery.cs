// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitRangeQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Comparisons.Queries.GetCommitRange;

/// <summary>Reads two commits and the comparison a notification stored between them, for the free comparison page.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Base">The full SHA of the commit compared to.</param>
/// <param name="Head">The full SHA of the commit compared.</param>
/// <remarks>
///   The database only, never computed (rule 3). Unlike <c>CompareCommits</c>, which the Api keeps answering 404
///   <c>Comparison.NotComputed</c>, a readable pair no notification compared answers with a <see langword="null" />
///   comparison. An unknown commit is still 404 <c>Commit.NotFound</c>.
/// </remarks>
public sealed record GetCommitRangeQuery(Forge Forge, string Owner, string Name, GitSha Base, GitSha Head) : IQuery<CommitRange>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
