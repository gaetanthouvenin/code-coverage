// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationUploadTokensQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Queries.ListOrganizationUploadTokens;

/// <summary>Lists the upload tokens of an organization.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
public sealed record ListOrganizationUploadTokensQuery(Forge Forge, string Login) : IQuery<IReadOnlyList<UploadToken>>, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
