// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageSeries.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.History;

/// <summary>The coverage history of one flag of a branch.</summary>
/// <param name="Flag">The flag, case-sensitive; the empty text for the whole project.</param>
/// <param name="Points">The points, the oldest first.</param>
public sealed record CoverageSeries(string Flag, IReadOnlyList<CoveragePoint> Points)
{
  #region Methods

  /// <summary>Builds the series of the flags asked from the totals read, one per flag even when it has no point.</summary>
  /// <param name="entries">The totals read, in any order.</param>
  /// <param name="flags">The flags asked, in their order.</param>
  /// <param name="interval">How the points are grouped.</param>
  /// <param name="from">The first instant kept, compared with the date of each point.</param>
  /// <param name="to">The last instant kept, compared with the date of each point.</param>
  /// <returns>The series, in the order of the flags.</returns>
  /// <remarks>
  ///   The date of a point is the date of its committer, else its finalization; the last value of a period is the one of
  ///   the latest date, then of the latest commit, so that both database engines give the same series.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="entries" /> or <paramref name="flags" /> is <see langword="null" />.</exception>
  public static IReadOnlyList<CoverageSeries> Build(
    IEnumerable<HistoryEntry> entries,
    IReadOnlyList<string> flags,
    HistoryInterval interval,
    DateTimeOffset from,
    DateTimeOffset to)
  {
    ArgumentNullException.ThrowIfNull(entries);
    ArgumentNullException.ThrowIfNull(flags);

    var start = from.ToUniversalTime();
    var end = to.ToUniversalTime();
    var kept = entries.Where(e => e.At >= start && e.At <= end)
                      .OrderBy(static e => e.At)
                      .ThenBy(static e => e.CommitId)
                      .ToList();

    return
    [
      .. flags.Select(flag => new CoverageSeries(flag, GroupPoints(kept.Where(e => string.Equals(e.Flag, flag, StringComparison.Ordinal)), interval))),
    ];
  }

  /// <summary>Gives the first instant of the period of a date.</summary>
  /// <param name="at">The date, in UTC.</param>
  /// <param name="interval">The period.</param>
  /// <returns>The first instant of the day or of the ISO week, in UTC; the date itself by commit.</returns>
  public static DateTimeOffset PeriodOf(DateTimeOffset at, HistoryInterval interval)
  {
    var utc = at.ToUniversalTime();
    var day = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);

    return interval switch
    {
      HistoryInterval.Day => day,
      HistoryInterval.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
      var _ => utc,
    };
  }

  private static List<CoveragePoint> GroupPoints(IEnumerable<HistoryEntry> ordered, HistoryInterval interval)
  {
    if (interval is HistoryInterval.Commit)
    {
      return [.. ordered.Select(static e => new CoveragePoint(e.At, e.Sha, e.Lines, e.Hits, e.Misses, e.Partials, e.Coverage))];
    }

    // The entries come the oldest first: the last one of a period is its value.
    return
    [
      .. ordered.GroupBy(e => PeriodOf(e.At, interval))
                .Select(static g => (Period: g.Key, Last: g.Last()))
                .Select(static p => new CoveragePoint(p.Period, p.Last.Sha, p.Last.Lines, p.Last.Hits, p.Last.Misses, p.Last.Partials, p.Last.Coverage)),
    ];
  }

  #endregion
}
