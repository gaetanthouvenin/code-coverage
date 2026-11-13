// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using Mediator;

using CodeCoverage.Application.Features.Repositories.Commands.ActivateRepository;
using CodeCoverage.Application.Features.Repositories.Commands.DeactivateRepository;
using CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepository;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepositoryPermission;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepositorySettings;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Application.Features.Repositories.Queries.ListRepositorySummaries;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Web.ViewModels.Commits;
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

  /// <summary>The fixed number of decimals of the coverage on the home page, which does not read every configuration.</summary>
  public const int HomePrecision = 2;

  /// <summary>The forge segment whose badges keep the addresses of Codecov.</summary>
  private static readonly string GitHubSegment = ForgeSegments.ToSegment(Forge.GitHub);

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
      return new RepositoryOverviewViewModel(repository.DefaultBranch, [], branches, named, ToFormat(CoverageConfig.Create(ConfigNode.Null)), repository.IsPrivate);
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
      ToFormat(config),
      repository.IsPrivate
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

  /// <summary>
  ///   Reads the repositories of one organization the user can read, each with the covered head of its default branch,
  ///   its flags and its daily coverage over 30 days, in one request: the page sends one request per organization.
  /// </summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the organization.</param>
  /// <param name="login">The login of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repositories by name; none when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<RepositorySummaryViewModel>> ListRepositorySummaryViewModelsAsync(
    this ISender sender,
    string forge,
    string login,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!TryParseForge(forge, out var parsed))
    {
      return [];
    }

    var summaries = await sender.Send(new ListRepositorySummariesQuery(parsed, login, null), cancellationToken).ConfigureAwait(false);

    return [.. summaries.OrderBy(static s => s.Repository.Name, StringComparer.OrdinalIgnoreCase).Select(s => ToSummary(forge, s))];
  }

  /// <summary>Tells whether the user administers a repository: the settings tab and its page are theirs only.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns><see langword="true" /> when the user administers it; false for a forge segment the console does not serve.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> CanAdministerRepositoryAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!TryParseForge(forge, out var parsed))
    {
      return false;
    }

    return await sender.Send(new GetRepositoryPermissionQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Reads the settings of a repository: its state and the configuration in force.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The settings, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<RepositorySettingsViewModel?> GetRepositorySettingsViewModelAsync(
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

    var settings = await sender.Send(new GetRepositorySettingsQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false);
    var tokens = await sender.ListRepositoryUploadTokenViewModelsAsync(forge, owner, name, cancellationToken).ConfigureAwait(false);
    var badgeToken = await sender.GetBadgeTokenAsync(forge, owner, name, cancellationToken).ConfigureAwait(false);

    return new RepositorySettingsViewModel(
      settings.Repository.Name,
      settings.Repository.DisabledAt is null,
      ToConfig(settings.Config),
      tokens,
      badgeToken
    );
  }

  /// <summary>Enables a repository again: its coverage is processed once more.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the repository is enabled.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> ActivateRepositoryAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!TryParseForge(forge, out var parsed))
    {
      return false;
    }

    await sender.Send(new ActivateRepositoryCommand(parsed, owner, name), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Disables a repository: its uploads are refused and nothing is published any more.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the repository is disabled.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> DeactivateRepositoryAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!TryParseForge(forge, out var parsed))
    {
      return false;
    }

    await sender.Send(new DeactivateRepositoryCommand(parsed, owner, name), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>The number of rows the panels of the overview show.</summary>
  public const int ActivityRows = 5;

  /// <summary>Reads what the lower half of the overview shows: the latest commits, the open pull requests and the badge.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="branch">The default branch, already read by the overview.</param>
  /// <param name="format">How the repository writes a coverage, already read by the overview.</param>
  /// <param name="apiPublicUrl">The public address of the Api, or <see langword="null" /> when it is not set.</param>
  /// <param name="webPublicUrl">The public address of the console, or <see langword="null" /> when it is not set.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The panels, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="format" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   One composed load: neither the repository nor its configuration is read again, since the overview already
  ///   passed what they say.
  /// </remarks>
  public static async Task<RepositoryActivityViewModel?> GetRepositoryActivityViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string branch,
    CoverageFormatViewModel format,
    string? apiPublicUrl,
    string? webPublicUrl,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(format);

    if (!TryParseForge(forge, out var parsed))
    {
      return null;
    }

    var commits = await sender.ListRecentCommitRowsAsync(parsed, owner, name, branch, format, ActivityRows, cancellationToken).ConfigureAwait(false);
    var pulls = await sender.ListOpenPullRequestRowsAsync(parsed, owner, name, format, ActivityRows, cancellationToken).ConfigureAwait(false);
    var token = await sender.GetBadgeTokenAsync(forge, owner, name, cancellationToken).ConfigureAwait(false);

    return new RepositoryActivityViewModel(commits, pulls, ToSnippets(forge, owner, name, token, apiPublicUrl, webPublicUrl));
  }

  // The badge of a GitHub repository keeps the address of Codecov, so a README only changes host; the other forges
  // carry their own segment (ADR-014). Without the public addresses, no snippet can be written at all.
  private static BadgeSnippetsViewModel? ToSnippets(string forge, string owner, string name, string? token, string? apiPublicUrl, string? webPublicUrl)
  {
    if (string.IsNullOrWhiteSpace(apiPublicUrl) || string.IsNullOrWhiteSpace(webPublicUrl))
    {
      return null;
    }

    var (api, web) = (apiPublicUrl.TrimEnd('/'), webPublicUrl.TrimEnd('/'));
    var address = $"{Uri.EscapeDataString(forge)}/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";
    var path = string.Equals(forge, GitHubSegment, StringComparison.Ordinal)
                 ? $"{api}/{address}/graph/badge.svg"
                 : $"{api}/api/v1/badges/{address}.svg";
    var image = token is null ? path : $"{path}?token={Uri.EscapeDataString(token)}";
    var page = $"{web}/{address}";

    return new BadgeSnippetsViewModel(image, page, $"[![coverage]({image})]({page})", $"<a href=\"{page}\"><img src=\"{image}\" alt=\"coverage\"></a>");
  }

  /// <summary>Reads a forge segment strictly: only the segment the console writes, never a default forge.</summary>
  /// <param name="segment">The segment of the address.</param>
  /// <param name="forge">The forge it names.</param>
  /// <returns><see langword="true" /> when the segment is the one of a forge.</returns>
  internal static bool TryParseForge([NotNullWhen(true)] string? segment, out Forge forge) =>
    ForgeSegments.TryParse(segment, out forge) && string.Equals(ForgeSegments.ToSegment(forge), segment, StringComparison.Ordinal);

  // The configuration as it is written: its source, where it was read and the YAML itself, never a merged rewrite.
  private static EffectiveConfigViewModel ToConfig(EffectiveConfig config) =>
    new(
      $"ConfigSource_{config.Source}",
      config.Path,
      config.Sha is { } sha ? DisplayFormatter.ShortSha(sha) : null,
      config.Yaml,
      [.. config.Warnings.Select(static w => new ConfigWarningViewModel(w.Code, w.Key, w.Detail))],
      config.Errors
    );

  // The change over the period: the last known coverage minus the first; unknown with fewer than two of them.
  private static decimal? ChangeOf(CoverageSeries series)
  {
    var known = series.Points.Where(static p => p.Coverage is not null).ToList();

    return known.Count < 2 ? null : known[^1].Coverage - known[0].Coverage;
  }

  // A fixed format on the home page: the repositories may each round differently, and their configurations are not read.
  private static RepositorySummaryViewModel ToSummary(string forge, RepositorySummary summary)
  {
    var repository = summary.Repository;
    var head = summary.CoveredHead;

    return new RepositorySummaryViewModel(
      forge,
      repository.Owner,
      repository.Name,
      repository.DisabledAt is not null,
      DisplayFormatter.Percent(head?.Coverage, HomePrecision, RoundingMode.Nearest),
      head?.Coverage,
      [.. summary.History.Select(static d => d.Coverage)],
      head is null ? null : DisplayFormatter.ShortSha(head.Sha),
      head is null ? null : CommitMapper.ToStatus(head.State),
      head?.CompletedAt,
      summary.Flags
    );
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
