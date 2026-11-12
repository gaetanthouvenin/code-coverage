// ------------------------------------------------------------------------------------------------
// <copyright file="AuthenticateApiTokenQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access.Queries.AuthenticateApiToken;

/// <summary>Handler for the <see cref="AuthenticateApiTokenQuery" /> query.</summary>
/// <param name="tokens">The repository of the personal tokens.</param>
/// <param name="users">The repository of the users.</param>
/// <param name="hasher">Hashes the value of the token.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The token is found by the SHA-256 of its value, through the unique index: no value is ever compared in clear. Its
///     use is recorded at most once a minute.
///   </para>
///   <para>
///     A user whose GitHub token is gone still authenticates: GitHub is not asked here, and the access checks then deny,
///     since the permissions read from GitHub age out on their own (<c>Access:HardExpiryMinutes</c>).
///   </para>
/// </remarks>
internal sealed class AuthenticateApiTokenQueryHandler(
  IApiTokenRepository tokens,
  IUserRepository users,
  ITokenHasher hasher,
  IClock clock) : IQueryHandler<AuthenticateApiTokenQuery, ApiTokenIdentity?>
{
  #region Implementation of IQueryHandler<AuthenticateApiTokenQuery,ApiTokenIdentity?>

  /// <inheritdoc />
  public async ValueTask<ApiTokenIdentity?> Handle(AuthenticateApiTokenQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (string.IsNullOrWhiteSpace(query.Value))
    {
      return null;
    }

    var now = clock.UtcNow;

    if (await tokens.FindByHashAsync(hasher.Hash(query.Value), cancellationToken).ConfigureAwait(false) is not { } token
        || !token.IsUsableAt(now)
        || await users.FindAsync(token.UserId, cancellationToken).ConfigureAwait(false) is not { } user)
    {
      return null;
    }

    await tokens.RecordUseAsync(token.Id, now, cancellationToken).ConfigureAwait(false);

    return new ApiTokenIdentity(token.Id, user.Id, user.Login, user.Forge, user.ExternalId, token.OrganizationId);
  }

  #endregion
}
