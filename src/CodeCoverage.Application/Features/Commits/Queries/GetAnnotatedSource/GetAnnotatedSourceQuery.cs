// ------------------------------------------------------------------------------------------------
// <copyright file="GetAnnotatedSourceQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Source;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Commits.Queries.GetAnnotatedSource;

/// <summary>Reads a source file at a commit, line by line with its coverage (ARCHITECTURE §10bis.3).</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Path">The path of the file, as the report of the commit holds it; a leading <c>/</c> is ignored.</param>
/// <param name="Flag">The flag the coverage is restricted to, or <see langword="null" /> for every session.</param>
/// <remarks>
///   The source is read live from the forge after the access check, through a cache bounded in size and time, and never
///   stored. The path must be one of the report: an unknown path is refused before the forge is asked anything.
/// </remarks>
public sealed record GetAnnotatedSourceQuery(Forge Forge, string Owner, string Name, GitSha Sha, string Path, string? Flag)
  : IQuery<AnnotatedSource>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
