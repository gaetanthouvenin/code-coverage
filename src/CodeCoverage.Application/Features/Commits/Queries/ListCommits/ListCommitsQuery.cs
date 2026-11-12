// ------------------------------------------------------------------------------------------------
// <copyright file="ListCommitsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Commits.Queries.ListCommits;

/// <summary>Lists one page of the commits of a repository, the most recently completed first.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Branch">The branch the commits were uploaded for, or <see langword="null" /> for every commit.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of commits of a page, 1 to <see cref="MaxPageSize" />.</param>
public sealed record ListCommitsQuery(Forge Forge, string Owner, string Name, string? Branch, int Page = 1, int PageSize = ListCommitsQuery.DefaultPageSize)
  : IQuery<CommitPage>, IRepositoryScoped
{
  #region Fields

  /// <summary>The number of commits of a page when none is asked.</summary>
  public const int DefaultPageSize = 25;

  /// <summary>The largest page that can be asked.</summary>
  public const int MaxPageSize = 100;

  #endregion

  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
