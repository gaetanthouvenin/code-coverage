// ------------------------------------------------------------------------------------------------
// <copyright file="CompareCommitsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Comparisons.Queries.CompareCommits;

/// <summary>Reads the comparison stored for a head commit against a base commit.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Base">The full SHA of the base commit.</param>
/// <param name="Head">The full SHA of the head commit.</param>
/// <remarks>
///   Only a comparison a notification stored for the head, against this base, is served: an arbitrary pair is never
///   computed in a read. The comparison of the push build comes before those of the pull requests.
/// </remarks>
public sealed record CompareCommitsQuery(Forge Forge, string Owner, string Name, GitSha Base, GitSha Head) : IQuery<ComparisonView>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
