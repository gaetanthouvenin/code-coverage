// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using Mediator;

using CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepository;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the repositories for the pages and converts them to view models: no page names a Domain type.</summary>
internal static class RepositoryMapper
{
  #region Fields

  /// <summary>The number of series a chart of history reads at most.</summary>
  public const int MaxChartFlags = GetCoverageHistoryQuery.MaxFlags;

  /// <summary>The longest range read one point a day; a longer one reads one point a week.</summary>
  public const int MaxDailyRange = 90;

  #endregion

  #region Methods

  /// <summary>Reads the overview of a repository: its cards with their change over 30 days, formatted as its configuration says.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The overview, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<RepositoryOverviewViewModel?> GetRepositoryOverviewViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!TryParseForge(forge, out var parsed))
    {
      return null;
    }

    var overview = await sender.Send(new GetRepositoryQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false);
    var repository = overview.Repository;

    IReadOnlyList<string> branches = [.. overview.Branches.Select(static b => b.Name)];
    IReadOnlyList<string> named = [.. overview.Flags.Where(static f => f.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    if (overview.Totals.Count == 0)
    {
      return new RepositoryOverviewViewModel(repository.DefaultBranch, [], branches, named, ToFormat(CoverageConfig.Create(ConfigNode.Null)));
    }

    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;
    string[] flags = [CommitFlagTotals.ProjectFlag, .. named];
    var changes = new Dictionary<string, decimal?>(StringComparer.Ordinal);

    // A history asks for 20 flags at most: every flag still gets its change, read 20 at a time.
    foreach (var chunk in flags.Chunk(GetCoverageHistoryQuery.MaxFlags))
    {
      var query = new GetCoverageHistoryQuery(parsed, owner, name, repository.DefaultBranch, chunk, null, null, GetCoverageHistoryQuery.DayInterval);

      foreach (var series in await sender.Send(query, cancellationToken).ConfigureAwait(false))
      {
        changes[series.Flag] = ChangeOf(series);
      }
    }

    return new RepositoryOverviewViewModel(
      repository.DefaultBranch,
      [.. flags.Select(flag => ToCard(flag, overview.Totals.FirstOrDefault(t => string.Equals(t.Flag, flag, StringComparison.Ordinal)), changes.GetValueOrDefault(flag), config))],
      branches,
      named,
      ToFormat(config)
    );
  }

  /// <summary>Writes a value of the chart of history as the cards write it, with the precision and rounding of the repository.</summary>
  /// <param name="value">The coverage in percent.</param>
  /// <param name="format">How the repository writes a coverage.</param>
  /// <returns>The text, with the separators and percent pattern of the culture.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="format" /> is <see langword="null" />.</exception>
  public static string FormatCoverage(double value, CoverageFormatViewModel format)
  {
    ArgumentNullException.ThrowIfNull(format);

    return DisplayFormatter.Percent((decimal)value, format.Precision, Enum.Parse<RoundingMode>(format.Rounding));
  }

  /// <summary>Reads the history of some flags of a branch: one point a day up to 90 days, one a week beyond.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="branch">The branch.</param>
  /// <param name="flags">The flags, an empty text for the project; 20 at most.</param>
  /// <param name="days">The number of days before <paramref name="now" />.</param>
  /// <param name="now">The end of the range.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>One series per flag, in the order asked; none when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="flags" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<CoverageSeriesViewModel>> GetCoverageSeriesViewModelsAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string branch,
    IReadOnlyList<string> flags,
    int days,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(flags);

    if (!TryParseForge(forge, out var parsed))
    {
      return [];
    }

    // A day a point keeps 20 flags over 90 days under the 10 000 rows of a history; 365 days read a week a point.
    var interval = days <= MaxDailyRange ? GetCoverageHistoryQuery.DayInterval : GetCoverageHistoryQuery.WeekInterval;
    var query = new GetCoverageHistoryQuery(parsed, owner, name, branch, flags, now.AddDays(-days), now, interval);
    var series = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return [.. series.Select(static s => new CoverageSeriesViewModel(s.Flag, [.. s.Points.Select(static p => new CoveragePointViewModel(p.At, p.Coverage))]))];
  }

  /// <summary>Reads a forge segment strictly: only the segment the console writes, never a default forge.</summary>
  /// <param name="segment">The segment of the address.</param>
  /// <param name="forge">The forge it names.</param>
  /// <returns><see langword="true" /> when the segment is the one of a forge.</returns>
  internal static bool TryParseForge([NotNullWhen(true)] string? segment, out Forge forge) =>
    ForgeSegments.TryParse(segment, out forge) && string.Equals(ForgeSegments.ToSegment(forge), segment, StringComparison.Ordinal);

  // The change over the period: the last known coverage minus the first; unknown with fewer than two of them.
  private static decimal? ChangeOf(CoverageSeries series)
  {
    var known = series.Points.Where(static p => p.Coverage is not null).ToList();

    return known.Count < 2 ? null : known[^1].Coverage - known[0].Coverage;
  }

  private static CoverageFormatViewModel ToFormat(CoverageConfig config) => new(config.Precision, config.Rounding.ToString());

  private static FlagCardViewModel ToCard(string flag, CommitFlagTotals? totals, decimal? change, CoverageConfig config)
  {
    // The card writes the size of the change; its arrow and colour give the direction.
    var delta = change is { } value ? DisplayFormatter.Number(Math.Abs(value), config.Precision, config.Rounding) : null;

    return new FlagCardViewModel(
      flag,
      DisplayFormatter.Percent(totals?.Coverage, config.Precision, config.Rounding),
      delta,
      change is { } signed ? Math.Sign(signed) : 0,
      totals is { IsCarriedForward: true, CarriedForwardFromSha: { } from } ? DisplayFormatter.ShortSha(from) : null
    );
  }

  #endregion
}
