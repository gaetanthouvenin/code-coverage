// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositoryPermissionQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetRepositoryPermission;

/// <summary>Tells whether the sender administers a repository it can read.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <remarks>
///   The console asks it to show what the sender may do (the settings tab, the actions of a commit, re-notifying a
///   pull request). It reads the one rule of <see cref="RepositoryAuthorization" />, the one the gate itself applies,
///   so that what is shown and what is allowed cannot diverge. A repository the sender cannot read never answers: it
///   stays the single « not found » of ADR-013.
/// </remarks>
public sealed record GetRepositoryPermissionQuery(Forge Forge, string Owner, string Name) : IQuery<bool>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
