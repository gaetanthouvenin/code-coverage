// ------------------------------------------------------------------------------------------------
// <copyright file="CommitMerger.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Application.Services;

/// <summary>Rebuilds the report of a commit and the totals of its flags from its sessions (ARCHITECTURE §6.5, steps 5 to 7).</summary>
internal sealed class CommitMerger
{
  #region Methods

  /// <summary>Merges the sessions that join the report of the project.</summary>
  /// <param name="config">The effective configuration of the commit.</param>
  /// <param name="sessions">The sessions, in any order.</param>
  /// <returns>The merged report, after <c>ignore</c>.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public CoverageReport MergeProject(CoverageConfig config, IReadOnlyList<SessionCoverage> sessions)
  {
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(sessions);

    return ReportView.Build(ToSessionReports(config, sessions), [], config.BuildPathFilter([])).Report;
  }

  /// <summary>Builds the totals of the project and of each flag seen in the sessions or declared in the configuration.</summary>
  /// <param name="commit">The commit, finalized.</param>
  /// <param name="config">The effective configuration.</param>
  /// <param name="sessions">The sessions.</param>
  /// <param name="merged">The merged report.</param>
  /// <returns>The totals, the row of the project first and then each flag in ordinal order.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public IReadOnlyList<CommitFlagTotals> BuildFlagTotals(
    Commit commit,
    CoverageConfig config,
    IReadOnlyList<SessionCoverage> sessions,
    CoverageReport merged)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(sessions);
    ArgumentNullException.ThrowIfNull(merged);

    var reports = ToSessionReports(config, sessions);
    var flags = sessions.SelectMany(static session => session.Session.Flags)
                        .Concat(config.ListDeclaredFlags())
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal);
    var totals = new List<CommitFlagTotals> { CommitFlagTotals.Create(commit, CommitFlagTotals.ProjectFlag, merged.Totals, false, null) };

    foreach (var flag in flags)
    {
      var view = ReportView.Build(reports, [flag], new PathPatternSet(config.ResolveFlag(flag).Paths));
      var carrying = sessions.Where(session => session.Session.Flags.Contains(flag, StringComparer.Ordinal)).OrderBy(static session => session.Session.Number).ToList();
      var isCarried = carrying.Count > 0 && carrying.TrueForAll(static session => session.Session.Kind is SessionKind.CarriedForward);

      totals.Add(CommitFlagTotals.Create(commit, flag, view.Totals, isCarried, isCarried ? carrying[0].Session.CarriedForwardFromSha : null));
    }

    return totals;
  }

  /// <summary>Gives the sessions in the form the views need.</summary>
  /// <param name="config">The effective configuration.</param>
  /// <param name="sessions">The sessions.</param>
  /// <returns>The sessions; one with a flag <c>joined: false</c> leaves the project, as in Codecov.</returns>
  private static IReadOnlyList<SessionReport> ToSessionReports(CoverageConfig config, IReadOnlyList<SessionCoverage> sessions)
  {
    return
    [
      .. sessions.Select(session => new SessionReport(
                           session.Session.Flags,
                           session.Report,
                           session.Session.Flags.All(flag => config.ResolveFlag(flag).Joined)
                         )),
    ];
  }

  #endregion
}
