// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageDetail.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Coverage;

/// <summary>The lines, branches and methods of a set of files, next to the coverage of Codecov.</summary>
/// <remarks>
///   These figures are extra and named (ADR-016): the coverage of Codecov,
///   <see cref="CoverageTotals.Coverage" />, stays the headline figure. A line counts as executed when it
///   ran at least once, partial lines included, as in ReportGenerator.
/// </remarks>
public readonly record struct CoverageDetail
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CoverageDetail" /> struct.</summary>
  /// <param name="executedLines">The number of lines that ran at least once.</param>
  /// <param name="coverableLines">The number of coverable lines.</param>
  /// <param name="coveredBranches">The number of branches taken.</param>
  /// <param name="totalBranches">The number of branches.</param>
  /// <param name="coveredMethods">
  ///   The number of methods that ran at least once, or <see langword="null" /> when the methods are
  ///   not known.
  /// </param>
  /// <param name="totalMethods">
  ///   The number of methods, or <see langword="null" /> when they are not known.
  /// </param>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   A counter is negative, or a covered counter exceeds its total.
  /// </exception>
  /// <exception cref="ArgumentException">Only one of the two method counters is known.</exception>
  public CoverageDetail(
    int executedLines,
    int coverableLines,
    int coveredBranches,
    int totalBranches,
    int? coveredMethods,
    int? totalMethods)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(executedLines);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(executedLines, coverableLines);
    ArgumentOutOfRangeException.ThrowIfNegative(coveredBranches);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(coveredBranches, totalBranches);
    ArgumentException.ThrowIf(
      coveredMethods.HasValue != totalMethods.HasValue,
      "The methods are either known with both counters, or not known.",
      nameof(totalMethods));

    if (coveredMethods is { } executed && totalMethods is { } total)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(executed, nameof(coveredMethods));
      ArgumentOutOfRangeException.ThrowIfGreaterThan(executed, total, nameof(coveredMethods));
    }

    ExecutedLines = executedLines;
    CoverableLines = coverableLines;
    CoveredBranches = coveredBranches;
    TotalBranches = totalBranches;
    CoveredMethods = coveredMethods;
    TotalMethods = totalMethods;
  }

  #endregion

  #region Properties

  /// <summary>Gets the number of lines that ran at least once, partial lines included.</summary>
  public int ExecutedLines { get; }

  /// <summary>Gets the number of coverable lines.</summary>
  public int CoverableLines { get; }

  /// <summary>Gets the number of branches taken.</summary>
  public int CoveredBranches { get; }

  /// <summary>Gets the number of branches.</summary>
  public int TotalBranches { get; }

  /// <summary>
  ///   Gets the number of methods that ran at least once, or <see langword="null" /> when the methods
  ///   are not known.
  /// </summary>
  public int? CoveredMethods { get; }

  /// <summary>Gets the number of methods, or <see langword="null" /> when they are not known.</summary>
  public int? TotalMethods { get; }

  /// <summary>Gets a value indicating whether the methods are known.</summary>
  public bool HasMethods => TotalMethods.HasValue;

  /// <summary>Gets the percentage of executed lines, or <see langword="null" /> without a line.</summary>
  public decimal? LineRatio => CoverageMath.Ratio(ExecutedLines, CoverableLines);

  /// <summary>Gets the percentage of branches taken, or <see langword="null" /> without a branch.</summary>
  public decimal? BranchRatio => CoverageMath.Ratio(CoveredBranches, TotalBranches);

  /// <summary>
  ///   Gets the percentage of executed methods, or <see langword="null" /> without a method or when the
  ///   methods are not known.
  /// </summary>
  public decimal? MethodRatio =>
    CoveredMethods is { } executed && TotalMethods is { } total ? CoverageMath.Ratio(executed, total) : null;

  #endregion

  #region Methods

  /// <summary>Adds the detail of other files, for a total.</summary>
  /// <param name="other">The detail to add.</param>
  /// <returns>
  ///   The sum, each counter held to the largest integer; its methods are not known when they are not
  ///   known on one side.
  /// </returns>
  public CoverageDetail Add(CoverageDetail other)
  {
    var hasMethods = HasMethods && other.HasMethods;

    return new CoverageDetail(
      Saturate((long)ExecutedLines + other.ExecutedLines),
      Saturate((long)CoverableLines + other.CoverableLines),
      Saturate((long)CoveredBranches + other.CoveredBranches),
      Saturate((long)TotalBranches + other.TotalBranches),
      hasMethods ? Saturate((long)CoveredMethods!.Value + other.CoveredMethods!.Value) : null,
      hasMethods ? Saturate((long)TotalMethods!.Value + other.TotalMethods!.Value) : null);
  }

  /// <summary>Holds a sum to the largest integer.</summary>
  /// <param name="sum">The sum, not negative.</param>
  /// <returns>The sum, or <see cref="int.MaxValue" /> when it is larger.</returns>
  /// <remarks>Held covered and total counters keep their order: the covered one never exceeds the total.</remarks>
  internal static int Saturate(long sum)
  {
    return (int)Math.Min(sum, int.MaxValue);
  }

  #endregion
}
