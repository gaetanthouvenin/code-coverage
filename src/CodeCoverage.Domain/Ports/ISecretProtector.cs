// ------------------------------------------------------------------------------------------------
// <copyright file="ISecretProtector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Encrypts the secrets kept in the database and decrypts them at the moment of use.</summary>
/// <remarks>
///   <para>
///     A protected value can be stored and shown in configuration, never the plain text: the GitHub
///     App private key, the webhook secret, the OAuth client secret, the badge tokens and the GitHub
///     tokens of the users.
///   </para>
///   <para>
///     The key ring is shared by every host of the instance: a value protected by the Api is
///     unprotected by the Web, and the reverse.
///   </para>
/// </remarks>
public interface ISecretProtector
{
  #region Methods

  /// <summary>Encrypts a secret.</summary>
  /// <param name="plaintext">The secret in clear.</param>
  /// <returns>The protected value, safe to store.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="plaintext" /> is <see langword="null" />.</exception>
  string Protect(string plaintext);

  /// <summary>Decrypts a value produced by <see cref="Protect" />.</summary>
  /// <param name="protectedValue">The protected value.</param>
  /// <returns>The secret in clear.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="protectedValue" /> is <see langword="null" />.</exception>
  /// <exception cref="System.Security.Cryptography.CryptographicException">
  ///   The value was altered, was protected by another key ring, or its key is no longer available.
  /// </exception>
  string Unprotect(string protectedValue);

  #endregion
}
