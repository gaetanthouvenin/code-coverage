// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationUploadTokensQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Queries.ListOrganizationUploadTokens;

/// <summary>Handler for the <see cref="ListOrganizationUploadTokensQuery" /> query.</summary>
/// <param name="tokens">The upload tokens.</param>
internal sealed class ListOrganizationUploadTokensQueryHandler(IUploadTokenRepository tokens)
  : IQueryHandler<ListOrganizationUploadTokensQuery, IReadOnlyList<UploadToken>>
{
  #region Implementation of IQueryHandler<ListOrganizationUploadTokensQuery,IReadOnlyList<UploadToken>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<UploadToken>> Handle(ListOrganizationUploadTokensQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return await tokens.ListOfOrganizationAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
