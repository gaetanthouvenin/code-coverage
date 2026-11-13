// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequest;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the comparisons the notifications stored, for the pull request and compare pages.</summary>
internal static class ComparisonMapper
{
  #region Methods

  /// <summary>Reads a pull request and the comparison stored for its head, formatted as the configuration says.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The pull request, or <see langword="null" /> when the address is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  /// <remarks>The database only: the comment link is built from the stored comment, never read from the forge.</remarks>
  public static async Task<PullRequestDetailViewModel?> GetPullRequestDetailViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    int number,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed) || number <= 0)
    {
      return null;
    }

    var detail = await sender.Send(new GetPullRequestQuery(parsed, owner, name, number), cancellationToken).ConfigureAwait(false);
    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;

    return ToViewModel(detail, parsed, owner, name, config);
  }

  /// <summary>Converts a comparison as the pages show it.</summary>
  /// <param name="view">The stored comparison.</param>
  /// <param name="baseBranch">The branch the head is compared to, or <see langword="null" /> for a push build.</param>
  /// <param name="headTitle">The title of the column of the head in the <c>diff</c> table.</param>
  /// <param name="expectedBase">The commit a base report is expected for, when there is none.</param>
  /// <param name="config">The configuration, for the precision and the rounding.</param>
  /// <returns>The comparison.</returns>
  internal static ComparisonViewModel ToViewModel(ComparisonView view, string? baseBranch, string headTitle, GitSha? expectedBase, CoverageConfig config)
  {
    var comparison = view.Comparison;
    var result = comparison.Result;

    // A base whose report is gone is no base, even when a commit was compared to (as the comment says).
    var comparedTo = result.Base is null ? null : comparison.ComparedToSha;
    var delta = result.Delta;
    var patch = result.Patch.Totals;
    var missed = result.Patch.Files.Where(static f => f.Totals.Misses + f.Totals.Partials > 0)
                       .OrderByDescending(static f => f.Totals.Misses + f.Totals.Partials)
                       .ThenBy(static f => f.Path, StringComparer.Ordinal)
                       .ToList();

    return new ComparisonViewModel(
      DisplayFormatter.Percent(result.Head.Coverage, config.Precision, config.Rounding),
      DisplayFormatter.Delta(delta, config.Precision, config.Rounding),
      delta is { } value ? Math.Sign(value) : 0,
      result.Base is { } @base ? DisplayFormatter.Percent(@base.Coverage, config.Precision, config.Rounding) : null,
      DisplayFormatter.Percent(patch.Coverage, config.Precision, config.Rounding),
      patch.Coverage is { } ratio ? (double)ratio : null,
      patch.Hits,
      patch.Lines,
      patch.Misses + patch.Partials,
      missed.Count,
      missed.Count == 0 ? null : missed[0].Path[(missed[0].Path.LastIndexOf('/') + 1)..],
      missed.Count == 0 ? 0 : missed[0].Totals.Misses + missed[0].Totals.Partials,
      result.IndirectChanges.Count,
      new BaseExplanationViewModel(
        DisplayFormatter.ShortSha(view.HeadSha),
        comparison.ForkPointSha is { } fork ? DisplayFormatter.ShortSha(fork) : null,
        baseBranch,
        comparedTo is null ? null : DisplayFormatter.ShortSha(comparedTo),
        comparedTo is null && expectedBase is not null ? DisplayFormatter.ShortSha(expectedBase) : null,
        comparison.BehindBy,
        // Without a fork point the hops count from the first parent: « before the merge base » would be untrue.
        comparedTo is null || comparison.ForkPointSha is null ? null : comparison.Hops
      ),
      DiffTableLayout.Lay(
        baseBranch ?? DisplayFormatter.ShortSha(comparedTo ?? view.HeadSha),
        headTitle,
        result.Base,
        result.Head,
        view.BaseFiles,
        view.HeadFiles,
        config.Precision,
        config.Rounding),
      view.IsStale
    );
  }

  private static PullRequestDetailViewModel ToViewModel(PullRequestDetail detail, Forge forge, string owner, string name, CoverageConfig config)
  {
    var pull = detail.PullRequest;
    var headTitle = string.Create(CultureInfo.InvariantCulture, $"#{pull.Number}");

    return new PullRequestDetailViewModel(
      pull.Number,
      pull.Title,
      pull.AuthorLogin,
      pull.HeadBranch,
      pull.BaseBranch,
      CommentUrl(forge, owner, name, pull),
      detail.Comparison is { } view ? ToViewModel(view, pull.BaseBranch, headTitle, pull.ForkPointSha ?? pull.BaseShaGitHub, config) : null
    );
  }

  // The stored comment, on GitHub only: no forge is asked, and GitHub Enterprise is not covered.
  private static string? CommentUrl(Forge forge, string owner, string name, PullRequest pull) =>
    forge is Forge.GitHub && pull.CommentId is { } comment
      ? string.Create(
        CultureInfo.InvariantCulture,
        $"https://github.com/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/pull/{pull.Number}#issuecomment-{comment}")
      : null;

  #endregion
}
