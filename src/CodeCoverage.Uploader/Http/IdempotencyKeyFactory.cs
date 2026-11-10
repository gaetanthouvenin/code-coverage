// ------------------------------------------------------------------------------------------------
// <copyright file="IdempotencyKeyFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

namespace CodeCoverage.Uploader.Http;

/// <summary>Computes the <c>Idempotency-Key</c> of an upload: a run and its retries send the same one (§5.1).</summary>
internal static class IdempotencyKeyFactory
{
  #region Methods

  /// <summary>Computes the key: <c>sha256(slug|sha|flags|name|buildId|jobId|sha256 of the files)</c>, as the server does.</summary>
  /// <param name="request">What the upload sends.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The key, in lower-case hexadecimal.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task<string> CreateAsync(UploadRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var hashes = new List<string>(request.Reports.Count);

    foreach (var report in request.Reports)
    {
      await using var file = File.OpenRead(report);

      hashes.Add(Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false)));
    }

    string[] fields =
    [
      request.Slug,
      request.Sha,
      string.Join(',', request.Flags.Order(StringComparer.Ordinal)),
      request.Name ?? string.Empty,
      request.BuildId ?? string.Empty,
      request.JobId ?? string.Empty,
      string.Join(',', hashes.Order(StringComparer.Ordinal)),
    ];

    return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', fields))));
  }

  #endregion
}
