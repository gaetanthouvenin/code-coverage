// ------------------------------------------------------------------------------------------------
// <copyright file="ITokenHasher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Hashes the upload and API tokens, which are stored by their hash only.</summary>
/// <remarks>
///   The hash is deterministic, so that a presented token is found by its hash (unique index on
///   <c>Hash</c>), and 32 bytes long (<c>binary(32)</c>). The tokens carry enough entropy for a fast
///   hash: no salt, no key stretching.
/// </remarks>
public interface ITokenHasher
{
  #region Methods

  /// <summary>Hashes a token.</summary>
  /// <param name="token">The token in clear.</param>
  /// <returns>The 32-byte hash of the token.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="token" /> is <see langword="null" />.</exception>
  byte[] Hash(string token);

  /// <summary>Compares two hashes in a time that does not depend on their content.</summary>
  /// <param name="left">The first hash.</param>
  /// <param name="right">The second hash.</param>
  /// <returns>
  ///   <see langword="true" /> when both hashes have the same length and the same bytes; otherwise,
  ///   <see langword="false" />.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="left" /> or <paramref name="right" /> is <see langword="null" />.
  /// </exception>
  bool FixedTimeEquals(byte[] left, byte[] right);

  #endregion
}
