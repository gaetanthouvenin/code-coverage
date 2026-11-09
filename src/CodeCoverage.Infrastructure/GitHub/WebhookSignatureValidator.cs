// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookSignatureValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Checks the <c>X-Hub-Signature-256</c> header: HMAC-SHA256 of the raw body.</summary>
/// <param name="options">The settings of the GitHub App, reloaded without a restart.</param>
/// <param name="protector">The protector of the stored secrets.</param>
/// <param name="meter">The metrics of the product.</param>
/// <remarks>
///   The previous secret stays valid during a rotation. A secret that is not configured matches nothing: an empty key
///   would make a signature anyone can compute.
/// </remarks>
internal sealed class WebhookSignatureValidator(
  IOptionsMonitor<GitHubOptions> options,
  ISecretProtector protector,
  CodeCoverageMeter meter) : IWebhookSignatureValidator
{
  #region Fields

  /// <summary>The start of the value of the header.</summary>
  private const string Prefix = "sha256=";

  #endregion

  #region Methods

  /// <summary>Reads the digest of a header.</summary>
  /// <param name="signature">The value of the header, or <see langword="null" />.</param>
  /// <param name="digest">Receives the digest.</param>
  /// <returns><see langword="true" /> when the header is well formed.</returns>
  private static bool TryReadDigest(string? signature, Span<byte> digest) =>
    signature is not null
    && signature.Length == Prefix.Length + (digest.Length * 2)
    && signature.StartsWith(Prefix, StringComparison.Ordinal)
    && Convert.FromHexString(signature.AsSpan(Prefix.Length), digest, out _, out _) is OperationStatus.Done;

  /// <summary>Tells whether a secret signed a body.</summary>
  /// <param name="protectedSecret">The protected secret, or <see langword="null" />.</param>
  /// <param name="payload">The raw body.</param>
  /// <param name="digest">The digest of the header.</param>
  /// <returns><see langword="true" /> when the digests are equal, compared in constant time.</returns>
  private bool Matches(string? protectedSecret, byte[] payload, ReadOnlySpan<byte> digest)
  {
    if (string.IsNullOrEmpty(protectedSecret))
    {
      return false;
    }

    var key = Encoding.UTF8.GetBytes(protector.Unprotect(protectedSecret));

    try
    {
      if (key.Length == 0)
      {
        return false;
      }

      Span<byte> computed = stackalloc byte[HMACSHA256.HashSizeInBytes];
      HMACSHA256.HashData(key, payload, computed);

      return CryptographicOperations.FixedTimeEquals(computed, digest);
    }
    finally
    {
      CryptographicOperations.ZeroMemory(key);
    }
  }

  #endregion

  #region Implementation of IWebhookSignatureValidator

  /// <inheritdoc />
  /// <exception cref="CryptographicException">A stored secret cannot be decrypted.</exception>
  public bool IsValid(byte[] payload, string? signature)
  {
    ArgumentNullException.ThrowIfNull(payload);

    var settings = options.CurrentValue;
    Span<byte> digest = stackalloc byte[HMACSHA256.HashSizeInBytes];

    // Not short-circuited on the header alone: a malformed header counts as a failure like a wrong one.
    if (TryReadDigest(signature, digest)
        && (Matches(settings.WebhookSecret, payload, digest) || Matches(settings.WebhookSecretPrevious, payload, digest)))
    {
      return true;
    }

    meter.WebhookSignatureFailure();

    return false;
  }

  #endregion
}
