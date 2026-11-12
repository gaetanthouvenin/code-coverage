// ------------------------------------------------------------------------------------------------
// <copyright file="GetCoverageHistoryQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;

/// <summary>Reads the coverage history of a branch, one series per flag asked.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Branch">The branch, or <see langword="null" /> for the default branch.</param>
/// <param name="Flags">The flags, case-sensitive; none for the whole project.</param>
/// <param name="From">The first instant, or <see langword="null" /> for 30 days before <paramref name="To" />.</param>
/// <param name="To">The last instant, or <see langword="null" /> for now.</param>
/// <param name="Interval">How the points are grouped: <c>commit</c>, <c>day</c> or <c>week</c>.</param>
public sealed record GetCoverageHistoryQuery(
  Forge Forge,
  string Owner,
  string Name,
  string? Branch,
  IReadOnlyList<string> Flags,
  DateTimeOffset? From,
  DateTimeOffset? To,
  string Interval = GetCoverageHistoryQuery.CommitInterval) : IQuery<IReadOnlyList<CoverageSeries>>, IRepositoryScoped
{
  #region Fields

  /// <summary>One point per commit, the grouping when none is asked.</summary>
  public const string CommitInterval = "commit";

  /// <summary>The last value of each day.</summary>
  public const string DayInterval = "day";

  /// <summary>The last value of each ISO week.</summary>
  public const string WeekInterval = "week";

  /// <summary>The number of flags a query can ask at most.</summary>
  public const int MaxFlags = 20;

  /// <summary>The number of totals read at most, the most recent kept.</summary>
  public const int MaxRows = 10_000;

  /// <summary>The length of the history when no start is asked.</summary>
  public static readonly TimeSpan DefaultSpan = TimeSpan.FromDays(30);

  #endregion

  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion

  #region Methods

  /// <summary>Reads the grouping asked.</summary>
  /// <param name="interval">The grouping, as the query holds it.</param>
  /// <param name="parsed">The grouping.</param>
  /// <returns>Whether the text names a grouping, case-sensitive.</returns>
  public static bool TryParseInterval(string? interval, out HistoryInterval parsed)
  {
    (var known, parsed) = interval switch
    {
      CommitInterval => (true, HistoryInterval.Commit),
      DayInterval => (true, HistoryInterval.Day),
      WeekInterval => (true, HistoryInterval.Week),
      var _ => (false, HistoryInterval.Commit),
    };

    return known;
  }

  #endregion
}
