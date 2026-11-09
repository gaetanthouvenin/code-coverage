// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeRepositoryUploadTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;

/// <summary>Revokes an upload token of a repository.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner of the repository.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="TokenId">The identifier of the token.</param>
public sealed record RevokeRepositoryUploadTokenCommand(Forge Forge, string Owner, string Name, long TokenId) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
