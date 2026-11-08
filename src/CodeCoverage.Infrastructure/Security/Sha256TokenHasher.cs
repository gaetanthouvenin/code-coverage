// ------------------------------------------------------------------------------------------------
// <copyright file="Sha256TokenHasher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Security;

/// <summary>Hashes the tokens with SHA-256 over their UTF-8 bytes.</summary>
/// <remarks>
///   The upload and API tokens are random values with at least 128 bits of entropy (or imported
///   Codecov UUIDs): a plain SHA-256 is enough and keeps the lookup by hash possible.
/// </remarks>
internal sealed class Sha256TokenHasher : ITokenHasher
{
  #region Implementation of ITokenHasher

  /// <inheritdoc />
  public byte[] Hash(string token)
  {
    ArgumentNullException.ThrowIfNull(token);

    return SHA256.HashData(Encoding.UTF8.GetBytes(token));
  }

  /// <inheritdoc />
  public bool FixedTimeEquals(byte[] left, byte[] right)
  {
    ArgumentNullException.ThrowIfNull(left);
    ArgumentNullException.ThrowIfNull(right);

    return CryptographicOperations.FixedTimeEquals(left, right);
  }

  #endregion
}
