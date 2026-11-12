// ------------------------------------------------------------------------------------------------
// <copyright file="ListApiTokensQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access.Queries.ListApiTokens;

/// <summary>Lists the personal tokens of the API of the signed-in user, the most recent first, revoked ones included.</summary>
/// <remarks>Never a value: only the hash is kept. A request authenticated by a token lists what that token reaches.</remarks>
public sealed record ListApiTokensQuery : IQuery<IReadOnlyList<ApiToken>>, IUserScoped;
