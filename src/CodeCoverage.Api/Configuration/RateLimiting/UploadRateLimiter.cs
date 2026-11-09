// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRateLimiter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;

namespace CodeCoverage.Api.Configuration.RateLimiting;

/// <summary>Limits the uploads per pair of a credential and a repository, in a fixed window of one minute.</summary>
/// <remarks>
///   The partition is known once the fields of the body are read, after the rate limiting middleware of the host: the
///   controller calls this limiter. A token of an organization therefore has one window per repository.
/// </remarks>
public sealed class UploadRateLimiter : IDisposable
{
  #region Fields

  private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

  private readonly PartitionedRateLimiter<string> _limiter;

  private readonly IOptionsMonitor<UploadOptions> _options;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UploadRateLimiter" /> class.</summary>
  /// <param name="options">The limits of the uploads.</param>
  /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
  public UploadRateLimiter(IOptionsMonitor<UploadOptions> options)
  {
    ArgumentNullException.ThrowIfNull(options);

    _options = options;
    _limiter = PartitionedRateLimiter.Create<string, string>(static key => RateLimitPartition.GetFixedWindowLimiter(
      key,
      static partition => new FixedWindowRateLimiterOptions
      {
        PermitLimit = int.Parse(partition.AsSpan(0, partition.IndexOf(':', StringComparison.Ordinal)), CultureInfo.InvariantCulture),
        Window = Window,
        QueueLimit = 0,
        AutoReplenishment = true,
      }
    ));
  }

  #endregion

  #region Methods

  /// <summary>Takes a place in the window of a credential and a repository.</summary>
  /// <param name="credential">The credential, hashed for the key.</param>
  /// <param name="slug">The repository, as received.</param>
  /// <param name="retryAfter">How long to wait when the place is refused.</param>
  /// <returns><see langword="true" /> when the upload may continue.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public bool TryAcquire(string credential, string slug, out TimeSpan retryAfter)
  {
    ArgumentNullException.ThrowIfNull(credential);
    ArgumentNullException.ThrowIfNull(slug);

    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
    var limit = _options.CurrentValue.RateLimitPerMinute.ToString(CultureInfo.InvariantCulture);

    // The limit is part of the key: a new value of the setting applies at once.
    using var lease = _limiter.AttemptAcquire($"{limit}:{hash}:{slug.ToLowerInvariant()}");

    retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? delay : Window;

    return lease.IsAcquired;
  }

  /// <inheritdoc />
  public void Dispose() => _limiter.Dispose();

  #endregion
}
