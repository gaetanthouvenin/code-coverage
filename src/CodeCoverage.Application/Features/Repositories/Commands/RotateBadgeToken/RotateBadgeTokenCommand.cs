// ------------------------------------------------------------------------------------------------
// <copyright file="RotateBadgeTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Commands.RotateBadgeToken;

/// <summary>Replaces the badge token of a repository by a new generated one: the previous value no longer reads the badge.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <remarks>Returns the new value in clear, this one time.</remarks>
public sealed record RotateBadgeTokenCommand(Forge Forge, string Owner, string Name) : ICommand<string>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
