// ------------------------------------------------------------------------------------------------
// <copyright file="FileIndexCodec.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Infrastructure.Reports.Codecs;

/// <summary>The blob that lists the files of a report and their counters, without their lines.</summary>
/// <remarks>
///   <para>
///     The header is the one of <see cref="CcrCodec" />, version 1, with the magic <c>CFI2</c>, and the
///     body is compressed in the same way. It holds:
///   </para>
///   <code>
///     varint fileCount
///     per file: varint byteLength, UTF-8 path, varint hits, misses, partials, branches, methods,
///               varint executedLines, coveredBranches, totalBranches, u8 flags (bit0 methods known),
///               [varint coveredMethods, totalMethods]
///   </code>
///   <para>
///     The number of lines is not stored: it is the sum of the hits, misses and partials, and it is
///     also the number of coverable lines of the detail (ADR-016). Only <c>CFI2</c> is written; an
///     index of magic <c>CFI1</c>, the five counters of Codecov alone, stays readable and gives files
///     without detail.
///   </para>
/// </remarks>
internal static class FileIndexCodec
{
  #region Fields

  // A path of at least one character after its length, then the five counters.
  private const int MinimumCountersEntryBytes = 7;

  // The same, then the three counters of the detail and the flags.
  private const int MinimumDetailEntryBytes = 11;

  private const byte MethodsKnown = 1;
  private const int KnownFlagBits = MethodsKnown;

  #endregion

  #region Properties

  private static ReadOnlySpan<byte> CountersMagic => "CFI1"u8;

  private static ReadOnlySpan<byte> DetailMagic => "CFI2"u8;

  #endregion

  #region Methods

  /// <summary>Encodes the counters and the detail of each file of a report.</summary>
  /// <param name="report">The report.</param>
  /// <returns>The blob, of magic <c>CFI2</c>.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="report" /> is <see langword="null" />.
  /// </exception>
  public static byte[] Encode(CoverageReport report)
  {
    ArgumentNullException.ThrowIfNull(report);

    var writer = new VarIntWriter();

    writer.WriteInt32(report.FileTotals.Count);

    foreach (var file in report.FileTotals)
    {
      InvalidOperationException.ThrowIf(file.Detail is null, "A file of a report always has its detail.");

      var detail = file.Detail.Value;

      writer.WriteString(file.Path);
      writer.WriteInt32(file.Totals.Hits);
      writer.WriteInt32(file.Totals.Misses);
      writer.WriteInt32(file.Totals.Partials);
      writer.WriteInt32(file.Totals.Branches);
      writer.WriteInt32(file.Totals.Methods);
      writer.WriteInt32(detail.ExecutedLines);
      writer.WriteInt32(detail.CoveredBranches);
      writer.WriteInt32(detail.TotalBranches);

      if (detail is { CoveredMethods: { } covered, TotalMethods: { } total })
      {
        writer.WriteByte(MethodsKnown);
        writer.WriteInt32(covered);
        writer.WriteInt32(total);
      }
      else
      {
        writer.WriteByte(0);
      }
    }

    return CcrCodec.Seal(DetailMagic, writer.WrittenSpan);
  }

  /// <summary>Decodes the counters of each file, and their detail when the index keeps it.</summary>
  /// <param name="blob">The blob, of magic <c>CFI2</c> or <c>CFI1</c>.</param>
  /// <returns>
  ///   The files, in the order of the report; without detail for an index of magic <c>CFI1</c>.
  /// </returns>
  /// <exception cref="InvalidDataException">The blob is not a valid file index.</exception>
  public static IReadOnlyList<FileTotals> Decode(ReadOnlySpan<byte> blob)
  {
    var hasDetail = blob.StartsWith(DetailMagic);
    var reader = new VarIntReader(CcrCodec.Open(hasDetail ? DetailMagic : CountersMagic, blob).Span);
    var files = new FileTotals[reader.ReadCount(hasDetail ? MinimumDetailEntryBytes : MinimumCountersEntryBytes)];
    string? previous = null;

    for (var i = 0; i < files.Length; i++)
    {
      var path = reader.ReadString();

      CcrCodec.ThrowIfInvalidPath(ref reader, path, previous);

      var totals = ReadTotals(ref reader);

      files[i] = new FileTotals(path, totals, hasDetail ? ReadDetail(ref reader, totals.Lines) : null);
      previous = path;
    }

    reader.ThrowIfNotAtEnd();

    return files;
  }

  /// <summary>Reads the five counters of Codecov of a file.</summary>
  /// <param name="reader">The reader.</param>
  /// <returns>The counters.</returns>
  private static CoverageTotals ReadTotals(ref VarIntReader reader)
  {
    var hits = reader.ReadInt32();
    var misses = reader.ReadInt32();
    var partials = reader.ReadInt32();
    var branches = reader.ReadInt32();
    var methods = reader.ReadInt32();

    if ((long)hits + misses + partials > int.MaxValue)
    {
      throw reader.Corrupted("the number of lines of a file is out of range");
    }

    return new CoverageTotals(hits, misses, partials, branches, methods);
  }

  /// <summary>Reads the detail of a file.</summary>
  /// <param name="reader">The reader.</param>
  /// <param name="lines">The number of coverable lines of the file.</param>
  /// <returns>The detail.</returns>
  private static CoverageDetail ReadDetail(ref VarIntReader reader, int lines)
  {
    var executed = reader.ReadInt32();
    var coveredBranches = reader.ReadInt32();
    var totalBranches = reader.ReadInt32();
    var flags = reader.ReadByte();

    if (executed > lines)
    {
      throw reader.Corrupted("a file has more executed lines than lines");
    }

    if (coveredBranches > totalBranches)
    {
      throw reader.Corrupted("a file has more covered branches than branches");
    }

    if ((flags & ~KnownFlagBits) != 0)
    {
      throw reader.Corrupted("a file has unknown flags");
    }

    if ((flags & MethodsKnown) == 0)
    {
      return new CoverageDetail(executed, lines, coveredBranches, totalBranches, null, null);
    }

    var coveredMethods = reader.ReadInt32();
    var totalMethods = reader.ReadInt32();

    if (coveredMethods > totalMethods)
    {
      throw reader.Corrupted("a file has more executed methods than methods");
    }

    return new CoverageDetail(executed, lines, coveredBranches, totalBranches, coveredMethods, totalMethods);
  }

  #endregion
}
