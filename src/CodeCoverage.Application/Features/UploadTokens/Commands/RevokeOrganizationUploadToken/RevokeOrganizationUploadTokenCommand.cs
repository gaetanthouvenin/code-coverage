// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeOrganizationUploadTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;

/// <summary>Revokes an upload token of an organization.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <param name="TokenId">The identifier of the token.</param>
public sealed record RevokeOrganizationUploadTokenCommand(Forge Forge, string Login, long TokenId) : ICommand, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
