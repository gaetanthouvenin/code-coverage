// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositorySettingsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetRepositorySettings;

/// <summary>Reads what the settings page of a repository shows: its state and the configuration in force.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <remarks>
///   Reserved to the administrators of the repository, which includes the administrators of its organization
///   (<see cref="RepositoryAuthorization" />). Read-only: no forge is called and nothing is written.
/// </remarks>
public sealed record GetRepositorySettingsQuery(Forge Forge, string Owner, string Name) : IQuery<RepositorySettings>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
