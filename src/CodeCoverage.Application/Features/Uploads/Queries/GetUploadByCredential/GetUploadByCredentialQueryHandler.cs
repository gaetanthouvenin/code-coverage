// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadByCredentialQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Uploads.Queries.GetUploadByCredential;

/// <summary>Handler for the <see cref="GetUploadByCredentialQuery" /> query.</summary>
/// <param name="authenticator">Checks the credential.</param>
/// <param name="uploads">The uploads of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
internal sealed class GetUploadByCredentialQueryHandler(UploadAuthenticator authenticator, IUploadRepository uploads, ICommitRepository commits)
  : IQueryHandler<GetUploadByCredentialQuery, Upload>
{
  #region Methods

  /// <summary>Builds the refusal of an upload that this credential cannot read.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException NotFound() => new(UploadErrors.NotFound, "The upload does not exist or cannot be read.");

  #endregion

  #region Implementation of IQueryHandler<GetUploadByCredentialQuery,Upload>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The credential proves nothing.</exception>
  /// <exception cref="NotFoundException">The upload is not one of this repository, or this credential cannot read it.</exception>
  public async ValueTask<Upload> Handle(GetUploadByCredentialQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (!RepositorySlug.TryFromAddress(query.Owner, query.Name, out var slug))
    {
      throw NotFound();
    }

    long? repositoryId;

    try
    {
      var authorization = await authenticator.AuthenticateAsync(query.Credential, query.Forge, slug, cancellationToken).ConfigureAwait(false);

      repositoryId = authorization.RepositoryId;
    }
    catch (ForbiddenException)
    {
      // Refusing the token of another repository with a 403 would confirm the address.
      throw NotFound();
    }

    var upload = await uploads.FindByPublicIdAsync(query.UploadId, cancellationToken).ConfigureAwait(false);

    if (upload is null || repositoryId is null)
    {
      throw NotFound();
    }

    var commit = await commits.FindAsync(upload.CommitId, cancellationToken).ConfigureAwait(false);

    return commit?.RepositoryId == repositoryId ? upload : throw NotFound();
  }

  #endregion
}
