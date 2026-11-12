// ------------------------------------------------------------------------------------------------
// <copyright file="GetEffectiveConfigQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;

/// <summary>Reads the effective configuration of a repository, at the covered head of its default branch.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <remarks>
///   Read-only: the configuration kept for the covered head of the default branch, else the YAML of the default branch
///   with the one of the organization of the repository, flagged as not resolved at a commit. No forge is called and
///   nothing is written.
/// </remarks>
public sealed record GetEffectiveConfigQuery(Forge Forge, string Owner, string Name) : IQuery<EffectiveConfig>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
