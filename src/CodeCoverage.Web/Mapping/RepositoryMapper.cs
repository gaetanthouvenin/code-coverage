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

    if (overview.Totals.Count == 0)
    {
      return new RepositoryOverviewViewModel(repository.DefaultBranch, []);
    }

    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;
    string[] flags = [CommitFlagTotals.ProjectFlag, .. overview.Flags.Where(static f => f.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
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
      [.. flags.Select(flag => ToCard(flag, overview.Totals.FirstOrDefault(t => string.Equals(t.Flag, flag, StringComparison.Ordinal)), changes.GetValueOrDefault(flag), config))]
    );
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
