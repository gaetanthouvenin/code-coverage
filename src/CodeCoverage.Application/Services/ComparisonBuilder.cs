// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonBuilder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>Builds the comparison of a commit with its base, and stores it.</summary>
/// <param name="resolver">What finds the base and the diffs.</param>
/// <param name="reader">What reads the sessions of a commit.</param>
/// <param name="views">The cache of the views of the reports.</param>
/// <param name="comparisons">The repository of the comparisons.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   The patch and the files are read with the patch diff, which starts at the fork point; the indirect
///   changes with the comparison diff, which starts at the base report (ARCHITECTURE §7.4). Both reports
///   are read with the configuration of the head commit: its ignored paths and its flags.
/// </remarks>
internal sealed class ComparisonBuilder(
  BaseResolver resolver,
  CommitReportReader reader,
  IReportViewCache views,
  IComparisonRepository comparisons,
  IClock clock)
{
  #region Methods

  /// <summary>Builds and stores the comparison of a commit.</summary>
  /// <param name="repository">The repository of the commit.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="commit">The commit, whose report is complete.</param>
  /// <param name="config">The effective configuration of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The comparison and what it was built from.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="UnprocessableEntityException">The report of a session of the commit is no longer stored.</exception>
  /// <exception cref="RetryLaterException">The base is still being processed.</exception>
  public async Task<ComparisonContext> BuildAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    CoverageConfig config,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(config);

    var resolution = await resolver.ResolveAsync(repository, forge, commit, config.AllowPseudoCompare, cancellationToken).ConfigureAwait(false);

    var headSessions = await reader.ReadAsync(commit.Id, cancellationToken).ConfigureAwait(false)
                       ?? throw new UnprocessableEntityException(CommitErrors.Compacted, "The coverage of a session of this commit no longer exists.");

    // A base whose report went away since it was chosen is no base: the comparison says so instead of failing.
    var comparedTo = resolution.ComparedTo;
    var baseSessions = comparedTo is null ? null : await reader.ReadAsync(comparedTo.Id, cancellationToken).ConfigureAwait(false);

    if (baseSessions is null && comparedTo is not null)
    {
      comparedTo = null;
      resolution = resolution with { ComparedTo = null, ComparisonDiff = BaseResolution.NoDiff(), Hops = null };
    }

    var headReports = CommitMerger.ToSessionReports(config, headSessions);
    var baseReports = baseSessions is null ? null : CommitMerger.ToSessionReports(config, baseSessions);
    var projectPaths = config.BuildPathFilter([]);

    var headView = views.GetOrCreate(commit.Id, commit.ReportRevision, headReports, [], projectPaths);
    var baseView = comparedTo is null || baseReports is null
                     ? null
                     : views.GetOrCreate(comparedTo.Id, comparedTo.ReportRevision, baseReports, [], projectPaths);

    var result = new Comparison(
      headView.Totals,
      baseView?.Totals,
      ComparisonCalculator.Patch(headView, resolution.PatchDiff),
      ComparisonCalculator.Files(baseView, headView, resolution.PatchDiff),
      ComparisonCalculator.Flags(
        baseReports,
        headReports,
        ComparisonCalculator.CarriedForwardFlags(headSessions.Select(static s => s.Session)),
        resolution.PatchDiff,
        flag => new PathPatternSet(config.ResolveFlag(flag).Paths)
      ),
      baseView is null ? [] : ChangeDetector.Detect(baseView.Report, headView.Report, resolution.ComparisonDiff),
      resolution.DiffIncomplete
    );

    var stored = new CommitComparison(
      commit.Id,
      resolution.PullRequest?.Number,
      resolution.ForkPoint,
      comparedTo?.Sha,
      commit.ReportRevision,
      comparedTo?.ReportRevision,
      resolution.PatchDiff.Source,
      result,
      clock.UtcNow
    );

    await comparisons.SaveAsync(stored, cancellationToken).ConfigureAwait(false);

    return new ComparisonContext(stored, resolution, headSessions, baseSessions);
  }

  #endregion
}