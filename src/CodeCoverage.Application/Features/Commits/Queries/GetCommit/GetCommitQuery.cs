// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Commits.Queries.GetCommit;

/// <summary>Reads a commit with its sessions, its uploads, the totals of its flags and its posted statuses.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <remarks>It decodes no report: a compacted commit is read like any other.</remarks>
public sealed record GetCommitQuery(Forge Forge, string Owner, string Name, GitSha Sha) : IQuery<CommitDetail>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
