// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionCredentialReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.Caching.Memory;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>
///   Reads a forge connection of the organization of the scope and decrypts its credential, for the adapters of every
///   forge other than GitHub (ADR-017).
/// </summary>
/// <param name="connections">The repository of the forge connections, a global table.</param>
/// <param name="organization">The organization the scope has entered.</param>
/// <param name="protector">The secret protector, which decrypts the credential.</param>
/// <param name="cache">The memory cache of the process, which keeps the decrypted credential.</param>
/// <remarks>
///   <para>
///     The connection is read at each call, so a connection of another organization or a suspended one is refused even
///     while its credential is in the cache. Only the decryption is cached, under a key that starts with the
///     organization of the scope, next to the protected value it comes from: a new credential is decrypted again.
///   </para>
///   <para>The adapters of Azure DevOps, Bitbucket and GitLab read their connections here, never by the repository.</para>
/// </remarks>
internal sealed class ForgeConnectionCredentialReader(
  IForgeConnectionRepository connections,
  ICurrentOrganization organization,
  ISecretProtector protector,
  IMemoryCache cache)
{
  #region Fields

  /// <summary>How long a decrypted credential stays in the cache.</summary>
  private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

  #endregion

  #region Methods

  /// <summary>Reads a connection of the organization of the scope, with its credential decrypted.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The connection and its credential, which the caller must not keep.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="BadGatewayException">
  ///   The scope has entered no organization, or the connection is unknown, belongs to another organization or is
  ///   suspended (<see cref="ForgeErrors.Unavailable" />).
  /// </exception>
  /// <exception cref="System.Security.Cryptography.CryptographicException">The credential cannot be decrypted.</exception>
  public async Task<ForgeConnectionCredential> ReadAsync(string connectionId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(connectionId);

    var connection = await connections.FindAsync(connectionId, cancellationToken).ConfigureAwait(false);

    BadGatewayException.ThrowIfConnectionUnusable(
      connection is null
      || !organization.IsKnown
      || connection.OrganizationId != organization.Id
      || connection.Status is ForgeConnectionStatus.Suspended,
      connectionId
    );

    return new ForgeConnectionCredential(
      connection.Id,
      connection.Forge,
      connection.Kind,
      connection.BaseAddress,
      connection.TenantId,
      Decrypt(connection)
    );
  }

  /// <summary>Decrypts the credential of a connection, or takes it from the cache when it did not change.</summary>
  /// <param name="connection">The connection, of the organization of the scope.</param>
  /// <returns>The decrypted credential.</returns>
  private string Decrypt(ForgeConnection connection)
  {
    var key = string.Create(CultureInfo.InvariantCulture, $"{connection.OrganizationId}:forge-connection-credential:{connection.Id}");

    if (cache.TryGetValue<(string Protected, string Plain)>(key, out var cached)
        && string.Equals(cached.Protected, connection.CredentialProtected, StringComparison.Ordinal))
    {
      return cached.Plain;
    }

    var plain = protector.Unprotect(connection.CredentialProtected);

    cache.Set(key, (connection.CredentialProtected, plain), CacheDuration);

    return plain;
  }

  #endregion
}
