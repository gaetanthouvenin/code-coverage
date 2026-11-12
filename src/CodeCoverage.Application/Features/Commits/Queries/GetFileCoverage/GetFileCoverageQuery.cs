// ------------------------------------------------------------------------------------------------
// <copyright file="GetFileCoverageQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Commits.Queries.GetFileCoverage;

/// <summary>Reads the coverage of one file of a commit, line by line.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Path">The path of the file, as the report stores it.</param>
/// <param name="Flag">The flag to read the file for, or <see langword="null" /> for the merged report.</param>
/// <remarks>A compacted commit answers 422 <c>Commit.Compacted</c>: only its totals are kept.</remarks>
public sealed record GetFileCoverageQuery(Forge Forge, string Owner, string Name, GitSha Sha, string Path, string? Flag)
  : IQuery<FileCoverageView>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
