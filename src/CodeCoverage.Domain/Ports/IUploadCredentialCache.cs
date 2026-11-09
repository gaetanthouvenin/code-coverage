// ------------------------------------------------------------------------------------------------
// <copyright file="IUploadCredentialCache.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Uploads;

namespace CodeCoverage.Domain.Ports;

/// <summary>Remembers for a short time who an upload token of the organization of the scope is.</summary>
/// <remarks>
///   Only the identity of the token is kept, so that its expiry and its repository are still checked at each
///   upload. A token revoked meanwhile stays known until its entry ends, 60 seconds at most.
/// </remarks>
public interface IUploadCredentialCache
{
  #region Methods

  /// <summary>Finds a token by its hash.</summary>
  /// <param name="hash">The hash of the token.</param>
  /// <returns>Its identity, or <see langword="null" /> when it is not remembered.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="hash" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The scope has not entered an organization.</exception>
  UploadTokenIdentity? Find(byte[] hash);

  /// <summary>Remembers a token.</summary>
  /// <param name="hash">The hash of the token.</param>
  /// <param name="identity">Its identity.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The scope has not entered an organization.</exception>
  void Store(byte[] hash, UploadTokenIdentity identity);

  #endregion
}
