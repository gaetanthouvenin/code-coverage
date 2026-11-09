// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizeUploadQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Uploads;

namespace CodeCoverage.Application.Features.Uploads.Queries.AuthorizeUpload;

/// <summary>Handler for the <see cref="AuthorizeUploadQuery" /> query.</summary>
/// <param name="authenticator">Checks the credential.</param>
internal sealed class AuthorizeUploadQueryHandler(UploadAuthenticator authenticator) : IQueryHandler<AuthorizeUploadQuery, UploadAuthorization>
{
  #region Implementation of IQueryHandler<AuthorizeUploadQuery,UploadAuthorization>

  /// <inheritdoc />
  public async ValueTask<UploadAuthorization> Handle(AuthorizeUploadQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return await authenticator.AuthenticateAsync(query.Credential, query.Forge, RepositorySlug.Parse(query.Slug), cancellationToken)
                              .ConfigureAwait(false);
  }

  #endregion
}
