// ------------------------------------------------------------------------------------------------
// <copyright file="IndirectHeadTotalsReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>
///   Reads, at read time, the totals of the head report for the files of the indirect changes of a comparison: the
///   stored comparison keeps only their change, and the page shows their coverage too.
/// </summary>
/// <remarks>The file index of the head commit only, the database: no line is decoded, nothing is stored.</remarks>
internal sealed class IndirectHeadTotalsReader(ICommitReportRepository reports, ICoverageReportCodec codec)
{
  #region Methods

  /// <summary>Reads the totals of the head for the indirect changes.</summary>
  /// <param name="headId">The identifier of the head commit.</param>
  /// <param name="comparison">The stored comparison.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The totals by path, for the paths the head report holds; empty without indirect change or index.</returns>
  public async Task<IReadOnlyDictionary<string, CoverageTotals>> ReadAsync(long headId, Comparison comparison, CancellationToken cancellationToken)
  {
    if (comparison.IndirectChanges.Count == 0 || await reports.GetFileIndexAsync(headId, cancellationToken).ConfigureAwait(false) is not { } index)
    {
      return new Dictionary<string, CoverageTotals>(StringComparer.Ordinal);
    }

    var wanted = comparison.IndirectChanges.Select(static c => c.Path).ToHashSet(StringComparer.Ordinal);

    return codec.DecodeFileIndex(index).Where(f => wanted.Contains(f.Path)).ToDictionary(static f => f.Path, static f => f.Totals, StringComparer.Ordinal);
  }

  #endregion
}
