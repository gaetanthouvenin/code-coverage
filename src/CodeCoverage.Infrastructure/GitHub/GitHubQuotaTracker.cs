// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubQuotaTracker.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net.Http.Headers;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>
///   Reads the quota of the rate limit headers of an answer of GitHub and hands it to the <see cref="ForgeQuotaTracker" />
///   under the forge <see cref="Forge.GitHub" />: it keeps no quota of its own.
/// </summary>
/// <param name="quotas">The tracker of the quotas of every forge.</param>
/// <param name="clock">The clock that dates the observations.</param>
internal sealed class GitHubQuotaTracker(ForgeQuotaTracker quotas, IClock clock)
{
  #region Fields

  /// <summary>The header of the requests left.</summary>
  public const string RemainingHeader = "x-ratelimit-remaining";

  /// <summary>The header of the requests of the whole window.</summary>
  public const string LimitHeader = "x-ratelimit-limit";

  /// <summary>The header of the moment the window starts again, in seconds since the Unix epoch.</summary>
  public const string ResetHeader = "x-ratelimit-reset";

  #endregion

  #region Methods

  /// <summary>Reads a numeric header.</summary>
  /// <param name="headers">The headers of the answer.</param>
  /// <param name="name">The name of the header.</param>
  /// <returns>The value, or <see langword="null" /> when the header is missing or is not a number.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="headers" /> is <see langword="null" />.</exception>
  public static long? ReadNumber(HttpResponseHeaders headers, string name)
  {
    ArgumentNullException.ThrowIfNull(headers);

    return headers.TryGetValues(name, out var values)
           && long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
             ? value
             : null;
  }

  /// <summary>Reports the quota an answer of GitHub carries, under its installation.</summary>
  /// <param name="installation">The installation, or <see cref="ForgeQuota.AppConnection" /> for the App itself.</param>
  /// <param name="headers">The headers of the answer.</param>
  /// <returns>The requests left, or <see langword="null" /> when the answer says nothing of them: nothing is reported then.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="headers" /> is <see langword="null" />.</exception>
  public long? Report(string installation, HttpResponseHeaders headers)
  {
    ArgumentNullException.ThrowIfNull(headers);

    if (ReadNumber(headers, RemainingHeader) is not { } remaining)
    {
      return null;
    }

    var reset = ReadNumber(headers, ResetHeader) is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : (DateTimeOffset?)null;
    quotas.Report(Forge.GitHub, installation, remaining, ReadNumber(headers, LimitHeader), reset, clock.UtcNow);

    return remaining;
  }

  #endregion
}
