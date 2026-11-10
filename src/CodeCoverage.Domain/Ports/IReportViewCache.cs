// ------------------------------------------------------------------------------------------------
// <copyright file="IReportViewCache.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Ports;

/// <summary>Keeps the views of a report that several rules of one notification ask for with the same filters.</summary>
/// <remarks>
///   <para>
///     A view is the merge of the sessions a rule selects, filtered by its paths: building it reads every
///     line of the report. A revision of a report never changes, so a view is kept by commit, revision, flags
///     and paths, inside the organization of the scope.
///   </para>
///   <para>
///     The sessions are part of what a view is kept by: the configuration of the commit that reads a report
///     decides the sessions <c>joined: false</c> leaves out of a view without flags, and the base of a
///     comparison is read with the configuration of its head. Two heads share the view of their base only
///     when they join the same sessions.
///   </para>
/// </remarks>
public interface IReportViewCache
{
  #region Methods

  /// <summary>Gets the view of a report for a selection of flags and paths, building it when it is not kept.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="revision">The revision of the report of the commit.</param>
  /// <param name="sessions">The sessions of that revision, in the order they are stored, each telling whether the configuration joins it.</param>
  /// <param name="flags">The flags selected, case-sensitive; none selects the sessions that are joined.</param>
  /// <param name="paths">The paths kept.</param>
  /// <returns>The view.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  ReportView GetOrCreate(long commitId, int revision, IReadOnlyList<SessionReport> sessions, IReadOnlyCollection<string> flags, PathPatternSet paths);

  #endregion
}