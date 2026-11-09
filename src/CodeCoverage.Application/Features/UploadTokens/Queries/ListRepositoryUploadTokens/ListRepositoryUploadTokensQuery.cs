// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositoryUploadTokensQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.UploadTokens.Queries.ListRepositoryUploadTokens;

/// <summary>Lists the upload tokens of a repository.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner of the repository.</param>
/// <param name="Name">The name of the repository.</param>
public sealed record ListRepositoryUploadTokensQuery(Forge Forge, string Owner, string Name) : IQuery<IReadOnlyList<UploadToken>>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
