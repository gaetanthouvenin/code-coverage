// ------------------------------------------------------------------------------------------------
// <copyright file="SetupTokenIssuer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers.Text;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>Holds the hash of the setup token, in the memory of the process.</summary>
/// <param name="hasher">The hasher of the tokens.</param>
/// <param name="logger">The logger that gives the token to whoever can read the logs of the host.</param>
/// <remarks>
///   Only the hash is kept. A restart forgets it, and the next request while the App is not created issues a new one.
///   The first-start wizard runs on one instance of the Web host: another instance would hold its own token.
/// </remarks>
internal sealed partial class SetupTokenIssuer(ITokenHasher hasher, ILogger<SetupTokenIssuer> logger) : ISetupTokenIssuer
{
  #region Fields

  /// <summary>The number of random bytes of a token.</summary>
  private const int TokenBytes = 32;

  /// <summary>Guards <see cref="_hash" />.</summary>
  private readonly Lock _gate = new();

  /// <summary>The hash of the token, or <see langword="null" /> while none is valid.</summary>
  private byte[]? _hash;

  #endregion

  #region Methods

  /// <summary>Writes the token to the logs.</summary>
  /// <param name="logger">The logger.</param>
  /// <param name="setupToken">The token, in clear.</param>
  [LoggerMessage(EventId = 2701, Level = LogLevel.Warning, Message = "The instance is not set up yet. Open /setup with this setup token: {SetupToken}")]
  private static partial void LogSetupToken(ILogger logger, string setupToken);

  #endregion

  #region Implementation of ISetupTokenIssuer

  /// <inheritdoc />
  public void EnsureIssued()
  {
    string token;

    lock (_gate)
    {
      if (_hash is not null)
      {
        return;
      }

      token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
      _hash = hasher.Hash(token);
    }

    LogSetupToken(logger, token);
  }

  /// <inheritdoc />
  public bool IsValid(string? token)
  {
    if (string.IsNullOrEmpty(token))
    {
      return false;
    }

    byte[]? expected;

    lock (_gate)
    {
      expected = _hash;
    }

    return expected is not null && hasher.FixedTimeEquals(hasher.Hash(token), expected);
  }

  /// <inheritdoc />
  public void Revoke()
  {
    lock (_gate)
    {
      _hash = null;
    }
  }

  #endregion
}
