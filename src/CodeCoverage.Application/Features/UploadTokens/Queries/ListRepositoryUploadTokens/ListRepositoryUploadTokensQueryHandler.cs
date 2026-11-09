// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositoryUploadTokensQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Queries.ListRepositoryUploadTokens;

/// <summary>Handler for the <see cref="ListRepositoryUploadTokensQuery" /> query.</summary>
/// <param name="service">What the messages of the upload tokens share.</param>
/// <param name="tokens">The upload tokens.</param>
internal sealed class ListRepositoryUploadTokensQueryHandler(UploadTokenService service, IUploadTokenRepository tokens)
  : IQueryHandler<ListRepositoryUploadTokensQuery, IReadOnlyList<UploadToken>>
{
  #region Implementation of IQueryHandler<ListRepositoryUploadTokensQuery,IReadOnlyList<UploadToken>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<UploadToken>> Handle(ListRepositoryUploadTokensQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repositoryId = await service.GetRepositoryIdAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);

    return await tokens.ListOfRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
