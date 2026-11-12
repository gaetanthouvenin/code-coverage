// ------------------------------------------------------------------------------------------------
// <copyright file="DeactivateRepositoryCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Commands.DeactivateRepository;

/// <summary>Disables a repository: its uploads are refused and it is no longer notified, its history stays readable.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <remarks>
///   A repository already disabled is left as it is, without an audit entry. The synchronization of the installation
///   and the webhooks never enable it again.
/// </remarks>
public sealed record DeactivateRepositoryCommand(Forge Forge, string Owner, string Name) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
