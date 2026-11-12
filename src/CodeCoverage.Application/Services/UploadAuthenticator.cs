// ------------------------------------------------------------------------------------------------
// <copyright file="UploadAuthenticator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Identity;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Uploads;

namespace CodeCoverage.Application.Services;

/// <summary>Checks the credential of an upload, in the uniform order of ADR-013.</summary>
/// <param name="organization">The organization of the scope, unset when the owner is not an active organization.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="oidc">The verifier of the OIDC tokens.</param>
/// <param name="tokens">The upload tokens of the organization.</param>
/// <param name="cache">The identities of the tokens seen recently.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="hasher">The hash of the tokens.</param>
/// <param name="clock">The clock.</param>
/// <remarks>Until a credential is proven, no answer depends on the state of the organization.</remarks>
internal sealed class UploadAuthenticator(
  ICurrentOrganization organization,
  IOrganizationDirectory directory,
  IOidcTokenValidator oidc,
  IUploadTokenRepository tokens,
  IUploadCredentialCache cache,
  IRepositoryRepository repositories,
  IForgeGatewayProvider gateways,
  ITokenHasher hasher,
  IClock clock)
{
  #region Methods

  /// <summary>Checks a credential for a repository.</summary>
  /// <param name="credential">The credential.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="slug">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the checks.</param>
  /// <returns>What the credential proved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="credential" /> or <paramref name="slug" /> is <see langword="null" />.</exception>
  /// <exception cref="UnauthorizedException">The credential proves nothing.</exception>
  /// <exception cref="ForbiddenException">The credential does not give access to this repository.</exception>
  /// <exception cref="NotFoundException">A valid OIDC token names a repository that cannot be reached.</exception>
  public async Task<UploadAuthorization> AuthenticateAsync(
    UploadCredential credential,
    Forge forge,
    RepositorySlug slug,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(credential);
    ArgumentNullException.ThrowIfNull(slug);

    return credential.Kind is UploadCredentialKind.Jwt
      ? await AuthenticateOidcAsync(credential.Value, forge, slug, cancellationToken).ConfigureAwait(false)
      : await AuthenticateTokenAsync(credential.Value, slug, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Builds the refusal of a credential that proves nothing.</summary>
  /// <returns>The exception.</returns>
  private static UnauthorizedException Invalid() => new(UploadErrors.TokenInvalid, "The upload token is not valid.");

  /// <summary>Builds the refusal of an OIDC token issued for something else.</summary>
  /// <param name="message">What differs.</param>
  /// <returns>The exception.</returns>
  private static ForbiddenException Mismatch(string message) => new(UploadErrors.OidcMismatch, message);

  /// <summary>Builds the refusal of a repository that cannot be reached.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException RepositoryNotFound() => new(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");

  /// <summary>Compares two identifiers given by a forge, whatever their case.</summary>
  /// <param name="left">The first identifier.</param>
  /// <param name="right">The second identifier.</param>
  /// <returns><see langword="true" /> when they are the same.</returns>
  private static bool SameId(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

  /// <summary>Checks an OIDC token.</summary>
  /// <param name="token">The token.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="slug">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the checks.</param>
  /// <returns>What the token proved.</returns>
  private async Task<UploadAuthorization> AuthenticateOidcAsync(string token, Forge forge, RepositorySlug slug, CancellationToken cancellationToken)
  {
    // The identifiers of the token only mean something on GitHub, and may exist on another forge.
    if (forge is not Forge.GitHub || await oidc.ValidateAsync(token, cancellationToken).ConfigureAwait(false) is not { } identity)
    {
      throw Invalid();
    }

    // A GitHub repository has no namespace: a slug that names one is never the repository of the token.
    if (!string.Equals(identity.Repository, slug.ToString(), StringComparison.OrdinalIgnoreCase))
    {
      throw Mismatch("The OIDC token was issued for another repository.");
    }

    if (!organization.IsKnown)
    {
      throw RepositoryNotFound();
    }

    await CheckAccountAsync(identity, cancellationToken).ConfigureAwait(false);

    var repository = await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
    var connection = repository is null
                       ? await ReadConnectionAsync(forge, slug, cancellationToken).ConfigureAwait(false)
                       : null;

    if (!SameId(identity.RepositoryId, repository?.ExternalId ?? connection!.RepositoryExternalId))
    {
      throw Mismatch("The OIDC token was issued for another repository, of the same name.");
    }

    return new UploadAuthorization(PrincipalKind.GitHubOidc, null, repository?.Id, connection);
  }

  /// <summary>Checks that an OIDC token was issued for the account of the organization of the scope.</summary>
  /// <param name="identity">What the token proves.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>A task that completes when the account is checked.</returns>
  private async Task CheckAccountAsync(GitHubOidcIdentity identity, CancellationToken cancellationToken)
  {
    var current = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) ?? throw RepositoryNotFound();

    if (!SameId(identity.RepositoryOwnerId, current.ExternalAccountId))
    {
      throw Mismatch("The OIDC token was issued for another account.");
    }
  }

  /// <summary>Asks the forge about a repository the instance does not track yet.</summary>
  /// <param name="forge">The forge.</param>
  /// <param name="slug">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>What the forge reported.</returns>
  private async Task<ForgeConnectionInfo> ReadConnectionAsync(Forge forge, RepositorySlug slug, CancellationToken cancellationToken) =>
    await gateways.GetGateway(forge).GetRepositoryConnectionAsync(slug.Owner, slug.Name, cancellationToken).ConfigureAwait(false)
    ?? throw RepositoryNotFound();

  /// <summary>Checks an upload token.</summary>
  /// <param name="value">The value of the token.</param>
  /// <param name="slug">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the checks.</param>
  /// <returns>What the token proved.</returns>
  private async Task<UploadAuthorization> AuthenticateTokenAsync(string value, RepositorySlug slug, CancellationToken cancellationToken)
  {
    if (!organization.IsKnown)
    {
      throw Invalid();
    }

    var identity = await FindIdentityAsync(hasher.Hash(value), cancellationToken).ConfigureAwait(false);

    if (identity.ExpiresAt <= clock.UtcNow)
    {
      throw Invalid();
    }

    var repository = await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false);

    ForbiddenException.ThrowIfUploadTokenNotForRepository(identity.Scope is UploadTokenScope.Repository && repository?.Id != identity.RepositoryId);

    var kind = identity.Scope is UploadTokenScope.Repository ? PrincipalKind.RepositoryToken : PrincipalKind.OrganizationToken;

    return new UploadAuthorization(kind, identity.TokenId, repository?.Id, null);
  }

  /// <summary>Finds who a token is, in the cache or in the database.</summary>
  /// <param name="hash">The hash of the token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The identity of a token that is neither revoked nor expired when it is read from the database.</returns>
  private async Task<UploadTokenIdentity> FindIdentityAsync(byte[] hash, CancellationToken cancellationToken)
  {
    if (cache.Find(hash) is { } cached)
    {
      return cached;
    }

    var token = await tokens.FindByHashAsync(hash, cancellationToken).ConfigureAwait(false);

    // An expired token counts as absent (ARCHITECTURE §5.1, step 4): it is neither remembered nor marked as used.
    if (token is not { RevokedAt: null } || token.ExpiresAt <= clock.UtcNow)
    {
      throw Invalid();
    }

    var identity = new UploadTokenIdentity(token.Id, token.Scope, token.RepositoryId, token.ExpiresAt);

    cache.Store(hash, identity);

    // Only on a read of the database: the cache already limits the writes of a process to one a minute.
    await tokens.RecordUseAsync(token.Id, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return identity;
  }

  #endregion
}
