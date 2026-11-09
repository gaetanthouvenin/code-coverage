// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizeUploadQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Uploads;

namespace CodeCoverage.Application.Features.Uploads.Queries.AuthorizeUpload;

/// <summary>Tells whether a credential may upload to a repository, before any byte of the body is stored.</summary>
/// <param name="Credential">The credential presented with the upload.</param>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Slug">The repository, as received.</param>
public sealed record AuthorizeUploadQuery(UploadCredential Credential, Forge Forge, string Slug) : IQuery<UploadAuthorization>, IPublicOwnerAddressed
{
  #region Properties

  /// <inheritdoc />
  public string Owner => RepositorySlug.TryParse(Slug, out var slug) ? slug.Owner : string.Empty;

  #endregion
}
