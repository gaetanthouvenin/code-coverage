// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuotaTracker.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>
///   Keeps in memory the last quota of each connection of each forge as the answers report it, with its limit and its
///   reset; the publisher writes the changed ones to the database every 15 seconds, never once per request.
/// </summary>
/// <remarks>
///   <para>
///     The one tracker of every forge: the rate limit handler of a forge reads the headers of its answers and reports
///     them here, without keeping a tracker of its own.
///   </para>
///   <para>
///     A report takes its version and stores its entry under one lock, and a read lists the entries and the version
///     under the same lock: a read never returns a version an entry not yet stored will carry, so no report is skipped
///     for good.
///   </para>
/// </remarks>
internal sealed class ForgeQuotaTracker
{
  #region Fields

  private readonly Lock _gate = new();

  private readonly Dictionary<(Forge Forge, string Connection), (ForgeQuota Quota, long Version)> _quotas = [];

  private long _version;

  #endregion

  #region Methods

  /// <summary>Records the quota an answer of a forge reported.</summary>
  /// <param name="forge">The forge that answered.</param>
  /// <param name="connection">The connection, or <see cref="ForgeQuota.AppConnection" />.</param>
  /// <param name="remaining">The requests left.</param>
  /// <param name="limit">The requests of the window, or <see langword="null" />.</param>
  /// <param name="resetAt">When the window starts again, or <see langword="null" />.</param>
  /// <param name="observedAt">When the answer came.</param>
  /// <exception cref="ArgumentException"><paramref name="connection" /> is <see langword="null" />, empty or white space.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="connection" /> is longer than <see cref="ForgeQuota.ConnectionMaxLength" />.</exception>
  public void Report(Forge forge, string connection, long remaining, long? limit, DateTimeOffset? resetAt, DateTimeOffset observedAt)
  {
    var quota = ForgeQuota.Create(forge, connection, remaining, limit, resetAt, observedAt);
    var key = (forge, connection);

    lock (_gate)
    {
      // Two answers may end in any order: an older one ending last never replaces the newer.
      if (_quotas.TryGetValue(key, out var known) && known.Quota.ObservedAt > quota.ObservedAt)
      {
        return;
      }

      _quotas[key] = (quota, ++_version);
    }
  }

  /// <summary>Lists the quotas reported after a version, and the version to ask from the next time.</summary>
  /// <param name="since">The version of the previous call, 0 the first time.</param>
  /// <returns>The quotas changed since then, and the current version.</returns>
  public (IReadOnlyList<ForgeQuota> Changed, long Version) ChangedSince(long since)
  {
    lock (_gate)
    {
      return ([.. _quotas.Values.Where(e => e.Version > since).Select(static e => e.Quota)], _version);
    }
  }

  #endregion
}
