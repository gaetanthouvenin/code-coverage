// ------------------------------------------------------------------------------------------------
// <copyright file="MemoryReportViewCache.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Caching.Memory;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Caching;

/// <summary>Keeps the views of the reports for 10 minutes, in the memory of the process.</summary>
/// <param name="cache">The cache of the process, shared by every organization.</param>
/// <param name="organization">The organization of the scope, which prefixes every key.</param>
/// <remarks>
///   The key holds the organization, the commit, the revision of its report, the sessions left out of a view
///   without flags, the flags and the paths: the view of one organization is never served to another
///   (ADR-013), a report finalized again is never read through a view of its former revision, and a report
///   read with another configuration is never read through the sessions the first one joined.
/// </remarks>
internal sealed class MemoryReportViewCache(IMemoryCache cache, ICurrentOrganization organization) : IReportViewCache
{
  #region Fields

  // Cannot appear in a flag nor in a pattern: two selections never give the same key.
  private const char Separator = '\u001f';

  private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

  #endregion

  #region Methods

  /// <summary>Builds the key of a view in the organization of the scope.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="revision">The revision of the report.</param>
  /// <param name="sessions">The sessions of the revision.</param>
  /// <param name="flags">The flags selected.</param>
  /// <param name="paths">The paths kept.</param>
  /// <returns>The key.</returns>
  private string KeyOf(long commitId, int revision, IReadOnlyList<SessionReport> sessions, IReadOnlyCollection<string> flags, PathPatternSet paths)
  {
    var key = new StringBuilder("report-view:");

    key.Append(organization.Id.ToString(CultureInfo.InvariantCulture))
       .Append(':')
       .Append(commitId.ToString(CultureInfo.InvariantCulture))
       .Append(':')
       .Append(revision.ToString(CultureInfo.InvariantCulture))
       .Append(':');

    // Only a view without flags reads what the configuration joins. The sessions of a revision keep their order,
    // so a position names a session.
    if (flags.Count == 0)
    {
      for (var index = 0; index < sessions.Count; index++)
      {
        if (!sessions[index].Joined)
        {
          key.Append(index.ToString(CultureInfo.InvariantCulture)).Append(Separator);
        }
      }
    }

    key.Append(':');

    // The order of the flags does not change the view; the order of the patterns does (the last match wins).
    foreach (var flag in flags.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
    {
      key.Append(flag).Append(Separator);
    }

    key.Append(':');

    foreach (var pattern in paths.Patterns)
    {
      key.Append(pattern.IsNegative ? '!' : '+').Append(pattern.Source).Append(Separator);
    }

    return key.ToString();
  }

  #endregion

  #region Implementation of IReportViewCache

  /// <inheritdoc />
  public ReportView GetOrCreate(long commitId, int revision, IReadOnlyList<SessionReport> sessions, IReadOnlyCollection<string> flags, PathPatternSet paths)
  {
    ArgumentNullException.ThrowIfNull(sessions);
    ArgumentNullException.ThrowIfNull(flags);
    ArgumentNullException.ThrowIfNull(paths);

    var key = KeyOf(commitId, revision, sessions, flags, paths);

    if (cache.TryGetValue<ReportView>(key, out var kept) && kept is not null)
    {
      return kept;
    }

    var view = ReportView.Build(sessions, flags, paths);

    cache.Set(key, view, Lifetime);

    return view;
  }

  #endregion
}