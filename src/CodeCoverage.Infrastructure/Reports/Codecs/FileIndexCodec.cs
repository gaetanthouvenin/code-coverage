// ------------------------------------------------------------------------------------------------
// <copyright file="FileIndexCodec.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Infrastructure.Reports.Codecs;

/// <summary>The blob that lists the files of a report and their counters, without their lines.</summary>
/// <remarks>
///   <para>
///     The header is the one of <see cref="CcrCodec" /> with the magic <c>CFI1</c>, and the body is
///     compressed in the same way. It holds:
///   </para>
///   <code>
///     varint fileCount
///     per file: varint byteLength, UTF-8 path, varint hits, misses, partials, branches, methods
///   </code>
///   <para>The number of lines is not stored: it is the sum of the hits, misses and partials.</para>
/// </remarks>
internal static class FileIndexCodec
{
  #region Fields

  // A path of at least one character after its length, then the five counters.
  private const int MinimumEntryBytes = 7;

  #endregion

  #region Properties

  private static ReadOnlySpan<byte> Magic => "CFI1"u8;

  #endregion

  #region Methods

  /// <summary>Encodes the counters of each file of a report.</summary>
  /// <param name="report">The report.</param>
  /// <returns>The blob.</returns>
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
      writer.WriteString(file.Path);
      writer.WriteInt32(file.Totals.Hits);
      writer.WriteInt32(file.Totals.Misses);
      writer.WriteInt32(file.Totals.Partials);
      writer.WriteInt32(file.Totals.Branches);
      writer.WriteInt32(file.Totals.Methods);
    }

    return CcrCodec.Seal(Magic, writer.WrittenSpan);
  }

  /// <summary>Decodes a blob.</summary>
  /// <param name="blob">The blob.</param>
  /// <returns>The counters of each file, in ordinal order of their paths.</returns>
  /// <exception cref="InvalidDataException">
  ///   The blob is truncated or corrupted, or its version is not supported.
  /// </exception>
  public static IReadOnlyList<FileTotals> Decode(ReadOnlySpan<byte> blob)
  {
    var reader = new VarIntReader(CcrCodec.Open(Magic, blob).Span);
    var files = new FileTotals[reader.ReadCount(MinimumEntryBytes)];
    string? previous = null;

    for (var i = 0; i < files.Length; i++)
    {
      var path = reader.ReadString();

      CcrCodec.ThrowIfInvalidPath(ref reader, path, previous);
      files[i] = new FileTotals(path, ReadTotals(ref reader));
      previous = path;
    }

    reader.ThrowIfNotAtEnd();

    return files;
  }

  /// <summary>Reads the counters of a file.</summary>
  /// <param name="reader">The reader.</param>
  /// <returns>The counters.</returns>
  /// <exception cref="InvalidDataException">The number of lines exceeds an integer.</exception>
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

  #endregion
}
