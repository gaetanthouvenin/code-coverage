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
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
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

    return ToViewModel(detail, parsed, owner, name, config, $"{Uri.EscapeDataString(forge)}/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}");
  }

  /// <summary>Converts a comparison as the pages show it.</summary>
  /// <param name="view">The stored comparison.</param>
  /// <param name="baseBranch">The branch the head is compared to, or <see langword="null" /> for a push build.</param>
  /// <param name="headTitle">The title of the column of the head in the <c>diff</c> table.</param>
  /// <param name="expectedBase">The commit a base report is expected for, when there is none.</param>
  /// <param name="config">The configuration, for the precision and the rounding.</param>
  /// <param name="root">The address of the repository, for the links to the annotated source.</param>
  /// <param name="pull">The pull request the links carry (<c>?pull=</c>), or <see langword="null" />.</param>
  /// <param name="headTotals">The totals of the head for the files of the indirect changes, or <see langword="null" />.</param>
  /// <returns>The comparison.</returns>
  internal static ComparisonViewModel ToViewModel(
    ComparisonView view,
    string? baseBranch,
    string headTitle,
    GitSha? expectedBase,
    CoverageConfig config,
    string root,
    int? pull,
    IReadOnlyDictionary<string, CoverageTotals>? headTotals)
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
      view.IsStale,
      view.HeadSha.Value,
      result.DiffIncomplete,
      [
        .. result.Files.OrderByDescending(static f => f.MissingPatchLines)
                 .ThenByDescending(static f => Math.Abs(f.Delta ?? 0))
                 .ThenBy(static f => f.Path, StringComparer.Ordinal)
                 .Select(f => ToFile(f, view.HeadSha, root, pull, result.Base is not null, config)),
      ],
      [.. result.Flags.OrderBy(static f => f.Flag, StringComparer.Ordinal).Select(f => ToFlag(f, config))],
      [
        .. result.IndirectChanges.OrderBy(static c => c.Path, StringComparer.Ordinal)
                 .Select(c => ToIndirect(c, view.HeadSha, root, pull, headTotals, config)),
      ]
    );
  }

  /// <summary>The address of the annotated source of a file: each segment escaped, the pull request carried.</summary>
  /// <param name="root">The address of the repository.</param>
  /// <param name="sha">The commit.</param>
  /// <param name="path">The path of the file.</param>
  /// <param name="pull">The pull request, or <see langword="null" />.</param>
  /// <returns>The relative address.</returns>
  internal static string SourceHref(string root, GitSha sha, string path, int? pull)
  {
    var address = $"{root}/commit/{sha.Value}/blob/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}";

    return pull is { } number ? string.Create(CultureInfo.InvariantCulture, $"{address}?pull={number}") : address;
  }

  private static ImpactedFileViewModel ToFile(ImpactedFile file, GitSha head, string root, int? pull, bool hasBase, CoverageConfig config)
  {
    var removed = file.Head is null;
    var delta = hasBase ? file.Delta : null;

    return new ImpactedFileViewModel(
      file.Path,
      removed ? null : SourceHref(root, head, file.Path, pull),
      file.Status is FileDiffStatus.Added,
      removed,
      DisplayFormatter.Percent(file.Head?.Coverage, config.Precision, config.Rounding),
      removed ? null : DisplayFormatter.Delta(delta, config.Precision, config.Rounding),
      delta is { } value ? Math.Sign(value) : 0,
      file.Patch.Lines > 0 ? DisplayFormatter.Percent(file.Patch.Coverage, config.Precision, config.Rounding) : null,
      file.MissingPatchLines
    );
  }

  // A carried flag was not measured by this commit: its change says nothing and is shown as a dash.
  private static FlagComparisonRowViewModel ToFlag(FlagComparison flag, CoverageConfig config)
  {
    var delta = flag.CarriedForward ? null : flag.Delta;

    return new FlagComparisonRowViewModel(
      flag.Flag,
      DisplayFormatter.Percent(flag.Head?.Coverage, config.Precision, config.Rounding),
      DisplayFormatter.Delta(delta, config.Precision, config.Rounding),
      delta is { } value ? Math.Sign(value) : 0,
      flag.CarriedForward
    );
  }

  private static IndirectChangeViewModel ToIndirect(
    IndirectChange change,
    GitSha head,
    string root,
    int? pull,
    IReadOnlyDictionary<string, CoverageTotals>? headTotals,
    CoverageConfig config)
  {
    var gone = change.Kind is IndirectChangeKind.Disappeared;
    var coverage = headTotals is not null && headTotals.TryGetValue(change.Path, out var totals) ? totals.Coverage : null;

    return new IndirectChangeViewModel(
      change.Path,
      gone ? null : SourceHref(root, head, change.Path, pull),
      DisplayFormatter.Percent(coverage, config.Precision, config.Rounding),
      DisplayFormatter.Delta(change.DeltaCoverage, config.Precision, config.Rounding),
      change.DeltaCoverage is { } value ? Math.Sign(value) : 0
    );
  }

  private static PullRequestDetailViewModel ToViewModel(PullRequestDetail detail, Forge forge, string owner, string name, CoverageConfig config, string root)
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
      detail.Comparison is { } view
        ? ToViewModel(view, pull.BaseBranch, headTitle, pull.ForkPointSha ?? pull.BaseShaGitHub, config, root, pull.Number, detail.IndirectHeadTotals)
        : null,
      pull.HeadSha.Value
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
