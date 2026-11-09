// ------------------------------------------------------------------------------------------------
// <copyright file="MemoryUploadCredentialCache.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Caching.Memory;

using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Remembers the upload tokens for 60 seconds, in the memory of the process.</summary>
/// <param name="cache">The cache of the process, shared by every organization.</param>
/// <param name="organization">The organization of the scope, which prefixes every key.</param>
/// <remarks>The same value imported by two organizations gives two entries (ADR-013).</remarks>
internal sealed class MemoryUploadCredentialCache(IMemoryCache cache, ICurrentOrganization organization) : IUploadCredentialCache
{
  #region Fields

  private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

  #endregion

  #region Methods

  /// <summary>Builds the key of a token in the organization of the scope.</summary>
  /// <param name="hash">The hash of the token.</param>
  /// <returns>The key.</returns>
  private string KeyOf(byte[] hash)
  {
    ArgumentNullException.ThrowIfNull(hash);

    return $"upload-credential:{organization.Id}:{Convert.ToHexString(hash)}";
  }

  #endregion

  #region Implementation of IUploadCredentialCache

  /// <inheritdoc />
  public UploadTokenIdentity? Find(byte[] hash) => cache.Get<UploadTokenIdentity>(KeyOf(hash));

  /// <inheritdoc />
  public void Store(byte[] hash, UploadTokenIdentity identity)
  {
    ArgumentNullException.ThrowIfNull(identity);

    cache.Set(KeyOf(hash), identity, Lifetime);
  }

  #endregion
}
