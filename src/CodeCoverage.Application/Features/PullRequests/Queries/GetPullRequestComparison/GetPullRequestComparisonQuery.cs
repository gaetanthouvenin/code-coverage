// ------------------------------------------------------------------------------------------------
// <copyright file="GetPullRequestComparisonQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequestComparison;

/// <summary>Reads the comparison stored for the current head of a pull request.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Number">The number of the pull request.</param>
/// <remarks>
///   Nothing is computed and nothing is queued: the notification of the head stores the comparison. A comparison whose
///   reports changed since is served as stale.
/// </remarks>
public sealed record GetPullRequestComparisonQuery(Forge Forge, string Owner, string Name, int Number) : IQuery<ComparisonView>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
