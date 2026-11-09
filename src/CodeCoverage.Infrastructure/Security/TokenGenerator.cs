// ------------------------------------------------------------------------------------------------
// <copyright file="TokenGenerator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers.Text;
using System.Security.Cryptography;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Security;

/// <summary>Generates the values of the tokens from the cryptographic generator of the system.</summary>
internal sealed class TokenGenerator : ITokenGenerator
{
  #region Fields

  /// <summary>The prefix of an upload token, which the secret scanners recognize.</summary>
  private const string UploadTokenPrefix = "cvc_";

  /// <summary>The number of random bytes of a token: 43 characters once encoded.</summary>
  private const int RandomBytes = 32;

  #endregion

  #region Implementation of ITokenGenerator

  /// <inheritdoc />
  public string NewBadgeToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RandomBytes));

  /// <inheritdoc />
  public string NewUploadToken() => UploadTokenPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RandomBytes));

  #endregion
}
