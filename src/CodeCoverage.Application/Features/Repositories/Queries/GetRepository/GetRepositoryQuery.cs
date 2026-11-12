// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositoryQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetRepository;

/// <summary>Reads a repository with its branches, its flags and the last totals of its default branch.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
public sealed record GetRepositoryQuery(Forge Forge, string Owner, string Name) : IQuery<RepositoryOverview>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
