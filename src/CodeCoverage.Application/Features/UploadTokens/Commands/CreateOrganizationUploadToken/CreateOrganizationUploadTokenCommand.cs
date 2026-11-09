// ------------------------------------------------------------------------------------------------
// <copyright file="CreateOrganizationUploadTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;

/// <summary>Creates an upload token for every repository of an organization.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <param name="Label">The name given to the token.</param>
/// <param name="ImportedValue">The value of a <c>CODECOV_TOKEN</c> to import, or <see langword="null" /> to generate one.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
public sealed record CreateOrganizationUploadTokenCommand(
  Forge Forge,
  string Login,
  string Label,
  string? ImportedValue,
  DateTimeOffset? ExpiresAt) : ICommand<IssuedToken>, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the imported value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(CreateOrganizationUploadTokenCommand)} {{ Login = {Login} }}";

  #endregion
}
