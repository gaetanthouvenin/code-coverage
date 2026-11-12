// ------------------------------------------------------------------------------------------------
// <copyright file="ListApiTokensQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access.Queries.ListApiTokens;

/// <summary>Handler for the <see cref="ListApiTokensQuery" /> query.</summary>
/// <param name="currentUser">The principal of the scope.</param>
/// <param name="tokens">The repository of the personal tokens.</param>
internal sealed class ListApiTokensQueryHandler(ICurrentUser currentUser, IApiTokenRepository tokens) : IQueryHandler<ListApiTokensQuery, IReadOnlyList<ApiToken>>
{
  #region Implementation of IQueryHandler<ListApiTokensQuery,IReadOnlyList<ApiToken>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<ApiToken>> Handle(ListApiTokensQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (currentUser.UserId is not { } userId)
    {
      return [];
    }

    var owned = await tokens.ListOfUserAsync(userId, cancellationToken).ConfigureAwait(false);

    return [.. owned.Where(currentUser.Reaches)];
  }

  #endregion
}
