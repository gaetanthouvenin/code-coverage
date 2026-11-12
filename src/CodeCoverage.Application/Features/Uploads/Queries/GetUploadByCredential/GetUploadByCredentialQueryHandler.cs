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
  #region Implementation of IQueryHandler<GetUploadByCredentialQuery,Upload>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The credential proves nothing.</exception>
  /// <exception cref="NotFoundException">The upload is not one of this repository, or this credential cannot read it.</exception>
  public async ValueTask<Upload> Handle(GetUploadByCredentialQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (!RepositorySlug.TryFromAddress(query.Owner, query.Name, out var slug))
    {
      NotFoundException.ThrowIfUploadNotFound(false);
    }

    long? repositoryId;

    try
    {
      var authorization = await authenticator.AuthenticateAsync(query.Credential, query.Forge, slug, cancellationToken).ConfigureAwait(false);

      repositoryId = authorization.RepositoryId;
    }
    catch (ForbiddenException)
    {
      // Refusing the token of another repository with a 403 would confirm the address: it answers as a missing upload.
      repositoryId = null;
    }

    var upload = await uploads.FindByPublicIdAsync(query.UploadId, cancellationToken).ConfigureAwait(false);
    var commit = upload is null || repositoryId is null ? null : await commits.FindAsync(upload.CommitId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfUploadNotFound(upload is not null && repositoryId is not null && commit?.RepositoryId == repositoryId);

    // The guard passes only with an upload.
    return upload!;
  }

  #endregion
}
