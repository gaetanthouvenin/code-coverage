// ------------------------------------------------------------------------------------------------
// <copyright file="ActivateRepositoryCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Commands.ActivateRepository;

/// <summary>Enables a repository an administrator disabled: its coverage is processed again.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <remarks>
///   A repository already enabled is left as it is, without an audit entry. A repository its installation no longer
///   serves answers as a missing one: only the installation brings it back.
/// </remarks>
public sealed record ActivateRepositoryCommand(Forge Forge, string Owner, string Name) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
