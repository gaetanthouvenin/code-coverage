// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubQuotaTracker.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>
///   Keeps in memory the last GitHub quota of each installation as the answers report it, with its limit and its reset;
///   the publisher writes the changed ones to the database every 15 seconds, never once per request.
/// </summary>
/// <remarks>
///   A report takes its version and stores its entry under one lock, and a read lists the entries and the version under
///   the same lock: a read never returns a version an entry not yet stored will carry, so no report is skipped for good.
/// </remarks>
internal sealed class GitHubQuotaTracker
{
  #region Fields

  private readonly Lock _gate = new();

  private readonly Dictionary<string, (GitHubQuota Quota, long Version)> _quotas = new(StringComparer.Ordinal);

  private long _version;

  #endregion

  #region Methods

  /// <summary>Records the quota an answer of GitHub reported.</summary>
  /// <param name="installation">The installation, or <see cref="GitHubQuota.AppInstallation" />.</param>
  /// <param name="remaining">The requests left.</param>
  /// <param name="limit">The requests of the window, or <see langword="null" />.</param>
  /// <param name="resetAt">When the window starts again, or <see langword="null" />.</param>
  /// <param name="observedAt">When the answer came.</param>
  public void Report(string installation, long remaining, long? limit, DateTimeOffset? resetAt, DateTimeOffset observedAt)
  {
    var quota = GitHubQuota.Create(installation, remaining, limit, resetAt, observedAt);

    lock (_gate)
    {
      // Two answers may end in any order: an older one ending last never replaces the newer.
      if (_quotas.TryGetValue(installation, out var known) && known.Quota.ObservedAt > observedAt)
      {
        return;
      }

      _quotas[installation] = (quota, ++_version);
    }
  }

  /// <summary>Lists the quotas reported after a version, and the version to ask from the next time.</summary>
  /// <param name="since">The version of the previous call, 0 the first time.</param>
  /// <returns>The quotas changed since then, and the current version.</returns>
  public (IReadOnlyList<GitHubQuota> Changed, long Version) ChangedSince(long since)
  {
    lock (_gate)
    {
      return ([.. _quotas.Values.Where(e => e.Version > since).Select(static e => e.Quota)], _version);
    }
  }

  #endregion
}
