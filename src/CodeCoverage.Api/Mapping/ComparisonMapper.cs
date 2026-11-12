// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Commits;
using CodeCoverage.Api.Contracts.Comparisons;
using CodeCoverage.Application.Features.Comparisons.Queries.CompareCommits;
using CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequestComparison;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the comparisons between the Domain and the API.</summary>
internal static class ComparisonMapper
{
  #region Methods

  /// <summary>Reads the comparison stored for the head of a pull request and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The comparison.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<ComparisonResponse> GetPullRequestComparisonResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    int number,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var query = new GetPullRequestComparisonQuery(AddressSegments.ToRepositoryForge(forge), owner, repo, number);

    return ToResponse(await sender.Send(query, cancellationToken).ConfigureAwait(false));
  }

  /// <summary>Reads the comparison stored for a head commit against a base commit and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="baseSha">The full SHA of the base commit.</param>
  /// <param name="headSha">The full SHA of the head commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The comparison.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<ComparisonResponse> GetComparisonResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string? baseSha,
    string? headSha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var query = new CompareCommitsQuery(
      AddressSegments.ToRepositoryForge(forge),
      owner,
      repo,
      AddressSegments.ToCommitSha(baseSha),
      AddressSegments.ToCommitSha(headSha)
    );

    return ToResponse(await sender.Send(query, cancellationToken).ConfigureAwait(false));
  }

  /// <summary>Converts the counters of a commit, a file or a flag for the API.</summary>
  /// <param name="totals">The counters.</param>
  /// <returns>The model.</returns>
  internal static CoverageTotalsModel ToModel(CoverageTotals totals) => new(totals.Lines, totals.Hits, totals.Misses, totals.Partials, totals.Coverage);

  private static ComparisonResponse ToResponse(ComparisonView view)
  {
    var comparison = view.Comparison;
    var result = comparison.Result;
    var isExact = comparison.ComparedToSha is not null && comparison.ComparedToSha == comparison.ForkPointSha;

    return new ComparisonResponse(
      comparison.PullNumber,
      new BaseResolutionModel(view.HeadSha.Value, comparison.ForkPointSha?.Value, comparison.ComparedToSha?.Value, isExact),
      ToModel(result.Head),
      result.Base is { } baseTotals ? ToModel(baseTotals) : null,
      result.Delta,
      new PatchModel(
        ToModel(result.Patch.Totals),
        [.. result.Patch.Files.OrderBy(static f => f.Path, StringComparer.Ordinal).Select(static f => new PatchFileModel(f.Path, ToModel(f.Totals), f.DiffUnavailable))]
      ),
      [.. result.Files.Select(ToModel)],
      [.. result.Flags.OrderBy(static f => f.Flag, StringComparer.Ordinal).Select(ToModel)],
      [.. result.IndirectChanges.Select(ToModel)],
      comparison.DiffSource.ToString(),
      result.DiffIncomplete,
      view.IsStale,
      comparison.ComputedAt
    );
  }

  private static ImpactedFileModel ToModel(ImpactedFile file) =>
    new(
      file.Path,
      file.PreviousPath,
      file.Status.ToString(),
      file.Base is { } baseTotals ? ToModel(baseTotals) : null,
      file.Head is { } headTotals ? ToModel(headTotals) : null,
      ToModel(file.Patch),
      file.Delta
    );

  private static FlagComparisonModel ToModel(FlagComparison flag) =>
    new(
      flag.Flag,
      flag.Base is { } baseTotals ? ToModel(baseTotals) : null,
      flag.Head is { } headTotals ? ToModel(headTotals) : null,
      ToModel(flag.Patch.Totals),
      flag.Delta,
      flag.CarriedForward
    );

  private static IndirectChangeModel ToModel(IndirectChange change) =>
    new(change.Path, change.Kind.ToString(), change.DeltaHits, change.DeltaMisses, change.DeltaPartials, change.DeltaCoverage);

  #endregion
}
