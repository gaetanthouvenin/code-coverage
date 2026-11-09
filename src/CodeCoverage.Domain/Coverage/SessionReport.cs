// ------------------------------------------------------------------------------------------------
// <copyright file="SessionReport.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The coverage of one session of a commit, with what decides the views it belongs to.</summary>
public sealed class SessionReport
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SessionReport" /> class.</summary>
  /// <param name="flags">The flags of the session.</param>
  /// <param name="report">Its coverage.</param>
  /// <param name="joined">
  ///   A value indicating whether it joins the merged report; <see langword="false" /> as soon as one
  ///   of its flags says <c>joined: false</c>, as in Codecov.
  /// </param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="flags" /> or <paramref name="report" /> is <see langword="null" />.
  /// </exception>
  public SessionReport(IReadOnlyList<string> flags, CoverageReport report, bool joined = true)
  {
    ArgumentNullException.ThrowIfNull(flags);
    ArgumentNullException.ThrowIfNull(report);

    Flags = [.. flags];
    Report = report;
    Joined = joined;
  }

  #endregion

  #region Properties

  /// <summary>Gets the flags of the session.</summary>
  public IReadOnlyList<string> Flags { get; }

  /// <summary>Gets its coverage.</summary>
  public CoverageReport Report { get; }

  /// <summary>Gets a value indicating whether it joins the merged report.</summary>
  public bool Joined { get; }

  #endregion
}
