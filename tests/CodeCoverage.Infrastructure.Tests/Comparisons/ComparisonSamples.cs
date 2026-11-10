// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonSamples.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Infrastructure.Tests.Comparisons;

// A comparison with every part filled, and one with every optional part empty.
public static class ComparisonSamples
{
  #region Properties

  public static Comparison Full { get; } = new(
    new CoverageTotals(7, 2, 1, 3, 4),
    new CoverageTotals(6, 4, 0, 2, 1),
    new PatchCoverage(
      new CoverageTotals(2, 1, 1, 1, 0),
      [new PatchFileCoverage("src/a.cs", new CoverageTotals(2, 1, 1, 1, 0), false), new PatchFileCoverage("src/big \"quoted\" é.cs", default, true)]
    ),
    [
      new ImpactedFile("src/a.cs", null, FileDiffStatus.Modified, new CoverageTotals(1, 1, 0, 0, 0), new CoverageTotals(2, 1, 1, 1, 0), new CoverageTotals(2, 1, 1, 1, 0)),
      new ImpactedFile("src/new.cs", "src/old.cs", FileDiffStatus.Renamed, new CoverageTotals(1, 0, 0, 0, 0), null, default),
      new ImpactedFile("src/added.cs", null, FileDiffStatus.Added, null, new CoverageTotals(0, 3, 0, 0, 0), new CoverageTotals(0, 3, 0, 0, 0)),
    ],
    [
      new FlagComparison("unit", new CoverageTotals(6, 4, 0, 0, 0), new CoverageTotals(7, 2, 1, 0, 0), new PatchCoverage(new CoverageTotals(2, 1, 1, 0, 0), [new PatchFileCoverage("src/a.cs", new CoverageTotals(2, 1, 1, 0, 0), false)]), false),
      new FlagComparison("Integration", null, new CoverageTotals(1, 0, 0, 0, 0), PatchCoverage.Empty, true),
      new FlagComparison("gone", new CoverageTotals(1, 0, 0, 0, 0), null, PatchCoverage.Empty, false),
    ],
    [
      new IndirectChange("src/far.cs", IndirectChangeKind.Changed, -2, 1, 1, -33.33334m),
      new IndirectChange("src/appeared.cs", IndirectChangeKind.Appeared, 3, 0, 0, null),
      new IndirectChange("src/gone.cs", IndirectChangeKind.Disappeared, -1, -1, 0, null),
    ],
    true
  );

  public static Comparison Empty { get; } = new(default, null, PatchCoverage.Empty, [], [], [], false);

  #endregion
}