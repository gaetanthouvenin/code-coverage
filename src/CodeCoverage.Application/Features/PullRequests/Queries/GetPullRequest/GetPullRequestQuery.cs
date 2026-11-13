// ------------------------------------------------------------------------------------------------
// <copyright file="GetPullRequestQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequest;

/// <summary>Reads a pull request and the comparison stored for its head, for its page.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Number">The number of the pull request.</param>
/// <remarks>
///   The database only, never computed. Unlike <c>GetPullRequestComparison</c>, which the Api keeps answering 404
///   <c>Comparison.NotComputed</c>, a readable pull request whose head has no comparison yet answers with a
///   <see langword="null" /> comparison: its page shows the pull request and says the comparison is pending.
/// </remarks>
public sealed record GetPullRequestQuery(Forge Forge, string Owner, string Name, int Number) : IQuery<PullRequestDetail>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
