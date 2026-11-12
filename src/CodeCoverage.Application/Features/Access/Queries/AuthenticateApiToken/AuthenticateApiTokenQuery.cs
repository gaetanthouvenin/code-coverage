// ------------------------------------------------------------------------------------------------
// <copyright file="AuthenticateApiTokenQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access.Queries.AuthenticateApiToken;

/// <summary>Tells who a personal token of the API authenticates.</summary>
/// <param name="Value">The value of the token, in clear (<c>cvp_…</c>).</param>
/// <remarks>
///   Sent by the authentication handler of the Api, before any user or organization is set, hence
///   <see cref="IAnonymousEntry" />: it reads global tables only. It returns <see langword="null" /> for an unknown,
///   revoked or expired token, or a token whose user no longer exists.
/// </remarks>
public sealed record AuthenticateApiTokenQuery(string Value) : IQuery<ApiTokenIdentity?>, IAnonymousEntry
{
  #region Methods

  /// <summary>Returns the name only: the value never reaches a log or a trace.</summary>
  /// <returns>The name of the query.</returns>
  public override string ToString() => nameof(AuthenticateApiTokenQuery);

  #endregion
}
