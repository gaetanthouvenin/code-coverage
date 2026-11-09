// ------------------------------------------------------------------------------------------------
// <copyright file="CcrCodec.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers;
using System.IO.Compression;
using System.Text;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Reports.Codecs;

/// <summary>The <c>CCR1</c> blob format of the coverage reports.</summary>
/// <remarks>
///   <para>
///     A blob starts with the magic <c>CCR1</c>, the format version and the flags, one byte each
///     after the magic; the body that follows is compressed with Brotli and holds:
///   </para>
///   <code>
///     varint stringCount, then per string: varint byteLength, UTF-8 bytes
///     varint fileCount
///     per file: varint pathIndex, varint lineCount
///     per line: varint deltaLine, u8 shape (bit0 branches, bit1 method, bit2 missing ids),
///               varint hits, [varint covered, varint total], [varint missingCount, varint index...]
///   </code>
///   <para>
///     The string table holds the paths, then the identifiers of the missing branches. Version 1 is
///     frozen by <c>tests/fixtures/ccr/v1-sample.bin</c>: blobs of that version stay readable
///     forever, and a change of layout takes a new version byte.
///   </para>
/// </remarks>
internal sealed class CcrCodec : ICoverageReportCodec
{
  #region Fields

  /// <summary>The largest body a blob can decompress to, far above a real report.</summary>
  public const int MaxBodyBytes = 1 << 30;

  private const int MagicLength = 4;
  private const int HeaderLength = 6;
  private const byte FormatVersion = 1;
  private const byte NoFlags = 0;
  private const int BrotliQuality = 5;
  private const int BrotliWindow = 22;
  private const int DecompressionChunkBytes = 64 * 1024;

  private const byte ShapeBranches = 1;
  private const byte ShapeMethod = 2;
  private const byte ShapeMissingIds = 4;
  private const int KnownShapeBits = ShapeBranches | ShapeMethod | ShapeMissingIds;

  private const int MinimumStringBytes = 1;
  private const int MinimumLineBytes = 3;
  private const int MinimumFileBytes = 5;
  private const int MinimumIndexBytes = 1;

  #endregion

  #region Properties

  private static ReadOnlySpan<byte> Magic => "CCR1"u8;

  #endregion

  #region Methods

  /// <summary>Adds the header to a body and compresses the body.</summary>
  /// <param name="magic">The four bytes that name the format.</param>
  /// <param name="body">The body.</param>
  /// <returns>The blob.</returns>
  /// <exception cref="InvalidOperationException">
  ///   The body exceeds <see cref="MaxBodyBytes" />, so the blob could never be read back, or it
  ///   cannot be compressed.
  /// </exception>
  public static byte[] Seal(ReadOnlySpan<byte> magic, ReadOnlySpan<byte> body)
  {
    if (body.Length > MaxBodyBytes)
    {
      throw new InvalidOperationException($"A coverage blob cannot exceed {MaxBodyBytes} bytes once decompressed.");
    }

    var blob = new byte[HeaderLength + BrotliEncoder.GetMaxCompressedLength(body.Length)];

    magic.CopyTo(blob);
    blob[MagicLength] = FormatVersion;
    blob[MagicLength + 1] = NoFlags;

    if (!BrotliEncoder.TryCompress(body, blob.AsSpan(HeaderLength), out var written, BrotliQuality, BrotliWindow))
    {
      throw new InvalidOperationException("The body of a coverage blob could not be compressed.");
    }

    return blob[..(HeaderLength + written)];
  }

  /// <summary>Checks the header of a blob and decompresses its body.</summary>
  /// <param name="magic">The four bytes that name the expected format.</param>
  /// <param name="blob">The blob.</param>
  /// <param name="maxBodyBytes">The largest accepted body.</param>
  /// <returns>The body.</returns>
  /// <exception cref="InvalidDataException">
  ///   The header is not the expected one, the version or the flags are not supported, or the
  ///   compressed stream is invalid, truncated, followed by other bytes or too large.
  /// </exception>
  public static ReadOnlyMemory<byte> Open(
    ReadOnlySpan<byte> magic,
    ReadOnlySpan<byte> blob,
    int maxBodyBytes = MaxBodyBytes)
  {
    var format = Encoding.ASCII.GetString(magic);

    if (blob.Length < HeaderLength)
    {
      throw new InvalidDataException($"The {format} blob is truncated: it is shorter than its header.");
    }

    if (!blob[..MagicLength].SequenceEqual(magic))
    {
      throw new InvalidDataException($"The data is not a {format} blob: its magic does not match.");
    }

    var version = blob[MagicLength];

    if (version != FormatVersion)
    {
      throw new InvalidDataException($"Version {version} of the {format} blob is not supported: this build reads version {FormatVersion}.");
    }

    var flags = blob[MagicLength + 1];

    if (flags != NoFlags)
    {
      throw new InvalidDataException($"The {format} blob has unknown flags: {flags}.");
    }

    return Decompress(blob[HeaderLength..], maxBodyBytes);
  }

  /// <summary>Checks the path of a file against the invariants of a report.</summary>
  /// <param name="reader">The reader, for the error.</param>
  /// <param name="path">The path.</param>
  /// <param name="previous">The path of the previous file, if any.</param>
  /// <exception cref="InvalidDataException">
  ///   The path is blank, or does not come after the previous one in ordinal order.
  /// </exception>
  public static void ThrowIfInvalidPath(ref VarIntReader reader, string path, string? previous)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      throw reader.Corrupted("a path is blank");
    }

    if (previous is not null && string.CompareOrdinal(previous, path) >= 0)
    {
      throw reader.Corrupted("the paths are not sorted and distinct");
    }
  }

  /// <summary>Decompresses a body, within a limit that a forged stream cannot exceed.</summary>
  /// <param name="compressed">The compressed stream.</param>
  /// <param name="maxBodyBytes">The largest accepted body.</param>
  /// <returns>The body.</returns>
  /// <exception cref="InvalidDataException">
  ///   The stream is invalid, truncated, followed by other bytes or too large.
  /// </exception>
  private static ReadOnlyMemory<byte> Decompress(ReadOnlySpan<byte> compressed, int maxBodyBytes)
  {
    // Not "using var": that local would be read-only, and each call would work on a copy of the struct.
    var decoder = default(BrotliDecoder);

    try
    {
      var output = new ArrayBufferWriter<byte>();

      while (true)
      {
        var span = output.GetSpan(DecompressionChunkBytes);
        var room = Math.Min(span.Length, maxBodyBytes - output.WrittenCount + 1);
        var status = decoder.Decompress(compressed, span[..room], out var consumed, out var written);

        compressed = compressed[consumed..];
        output.Advance(written);

        if (output.WrittenCount > maxBodyBytes)
        {
          throw new InvalidDataException($"The body of the coverage blob exceeds {maxBodyBytes} bytes.");
        }

        switch (status)
        {
          case OperationStatus.Done when compressed.IsEmpty:
            return output.WrittenMemory;
          case OperationStatus.Done:
            throw new InvalidDataException("The coverage blob is corrupted: bytes follow its compressed body.");
          case OperationStatus.NeedMoreData:
            throw new InvalidDataException("The coverage blob is truncated: its compressed body ends too early.");
          case OperationStatus.InvalidData:
            throw new InvalidDataException("The coverage blob is corrupted: its compressed body is invalid.");
        }
      }
    }
    finally
    {
      decoder.Dispose();
    }
  }

  /// <summary>Lists the distinct strings of a report: the paths, then the branch identifiers.</summary>
  /// <param name="report">The report.</param>
  /// <param name="indexes">Receives the index of each string.</param>
  /// <returns>The strings, in the order of their indexes.</returns>
  private static List<string> BuildStringTable(CoverageReport report, Dictionary<string, int> indexes)
  {
    var table = new List<string>(report.Files.Count);

    foreach (var file in report.Files)
    {
      Intern(file.Path, table, indexes);
    }

    foreach (var missing in report.Files.SelectMany(file => file.Lines).Select(line => line.MissingBranches))
    {
      foreach (var identifier in missing ?? [])
      {
        Intern(identifier, table, indexes);
      }
    }

    return table;
  }

  /// <summary>Adds a string to the table unless it is already there.</summary>
  /// <param name="value">The string.</param>
  /// <param name="table">The table.</param>
  /// <param name="indexes">The index of each string of the table.</param>
  private static void Intern(string value, List<string> table, Dictionary<string, int> indexes)
  {
    if (indexes.TryAdd(value, table.Count))
    {
      table.Add(value);
    }
  }

  /// <summary>Writes the lines of a file.</summary>
  /// <param name="writer">The writer.</param>
  /// <param name="lines">The lines, in ascending order.</param>
  /// <param name="indexes">The index of each string of the table.</param>
  private static void WriteLines(
    VarIntWriter writer,
    IReadOnlyList<LineCoverage> lines,
    Dictionary<string, int> indexes)
  {
    writer.WriteInt32(lines.Count);

    var previous = 0;

    foreach (var line in lines)
    {
      writer.WriteInt32(line.Line - previous);
      previous = line.Line;
      WriteLineContent(writer, line, indexes);
    }
  }

  /// <summary>Writes what follows the number of a line.</summary>
  /// <param name="writer">The writer.</param>
  /// <param name="line">The line.</param>
  /// <param name="indexes">The index of each string of the table.</param>
  private static void WriteLineContent(VarIntWriter writer, LineCoverage line, Dictionary<string, int> indexes)
  {
    var hasBranches = line.Covered != 0 || line.Total != 0;
    var shape = (hasBranches ? ShapeBranches : 0)
      | (line.IsMethod ? ShapeMethod : 0)
      | (line.MissingBranches is null ? 0 : ShapeMissingIds);

    writer.WriteByte((byte)shape);
    writer.WriteUInt32((uint)Math.Clamp(line.Hits, 0, uint.MaxValue));

    if (hasBranches)
    {
      writer.WriteInt32(line.Covered);
      writer.WriteInt32(line.Total);
    }

    if (line.MissingBranches is not null)
    {
      writer.WriteInt32(line.MissingBranches.Length);

      foreach (var identifier in line.MissingBranches)
      {
        writer.WriteInt32(indexes[identifier]);
      }
    }
  }

  /// <summary>Reads the string table.</summary>
  /// <param name="reader">The reader.</param>
  /// <returns>The strings.</returns>
  private static string[] ReadStrings(ref VarIntReader reader)
  {
    var strings = new string[reader.ReadCount(MinimumStringBytes)];

    for (var i = 0; i < strings.Length; i++)
    {
      strings[i] = reader.ReadString();
    }

    return strings;
  }

  /// <summary>Reads a reference to the string table.</summary>
  /// <param name="reader">The reader.</param>
  /// <param name="strings">The string table.</param>
  /// <returns>The string.</returns>
  private static string ReadReference(ref VarIntReader reader, string[] strings)
  {
    var index = reader.ReadInt32();

    return index < strings.Length ? strings[index] : throw reader.Corrupted("a string index is out of range");
  }

  /// <summary>Reads the lines of a file.</summary>
  /// <param name="reader">The reader.</param>
  /// <param name="strings">The string table.</param>
  /// <returns>The lines, which cannot be empty and are strictly ascending.</returns>
  private static LineCoverage[] ReadLines(ref VarIntReader reader, string[] strings)
  {
    var count = reader.ReadCount(MinimumLineBytes);

    if (count == 0)
    {
      throw reader.Corrupted("a file has no line");
    }

    var lines = new LineCoverage[count];
    var number = 0L;

    for (var i = 0; i < lines.Length; i++)
    {
      var delta = reader.ReadUInt32();

      if (delta == 0)
      {
        throw reader.Corrupted("the line numbers do not increase");
      }

      number += delta;

      if (number > int.MaxValue)
      {
        throw reader.Corrupted("a line number is out of range");
      }

      lines[i] = ReadLineContent(ref reader, (int)number, strings);
    }

    return lines;
  }

  /// <summary>Reads what follows the number of a line.</summary>
  /// <param name="reader">The reader.</param>
  /// <param name="number">The number of the line.</param>
  /// <param name="strings">The string table.</param>
  /// <returns>The line.</returns>
  private static LineCoverage ReadLineContent(ref VarIntReader reader, int number, string[] strings)
  {
    var shape = reader.ReadByte();

    if ((shape & ~KnownShapeBits) != 0)
    {
      throw reader.Corrupted("a line has an unknown shape");
    }

    long hits = reader.ReadUInt32();
    var covered = 0;
    var total = 0;

    if ((shape & ShapeBranches) != 0)
    {
      covered = reader.ReadInt32();
      total = reader.ReadInt32();
    }

    var missing = (shape & ShapeMissingIds) != 0 ? ReadMissingBranches(ref reader, strings) : null;

    return new LineCoverage(number, hits, covered, total, (shape & ShapeMethod) != 0, missing);
  }

  /// <summary>Reads the identifiers of the missing branches of a line.</summary>
  /// <param name="reader">The reader.</param>
  /// <param name="strings">The string table.</param>
  /// <returns>The identifiers, possibly none.</returns>
  private static string[] ReadMissingBranches(ref VarIntReader reader, string[] strings)
  {
    var missing = new string[reader.ReadCount(MinimumIndexBytes)];

    for (var i = 0; i < missing.Length; i++)
    {
      missing[i] = ReadReference(ref reader, strings);
    }

    return missing;
  }

  #endregion

  #region Implementation of ICoverageReportCodec

  /// <inheritdoc />
  public byte[] Encode(CoverageReport report)
  {
    ArgumentNullException.ThrowIfNull(report);

    var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
    var table = BuildStringTable(report, indexes);
    var writer = new VarIntWriter();

    writer.WriteInt32(table.Count);

    foreach (var value in table)
    {
      writer.WriteString(value);
    }

    writer.WriteInt32(report.Files.Count);

    foreach (var file in report.Files)
    {
      writer.WriteInt32(indexes[file.Path]);
      WriteLines(writer, file.Lines, indexes);
    }

    return Seal(Magic, writer.WrittenSpan);
  }

  /// <inheritdoc />
  public CoverageReport Decode(ReadOnlyMemory<byte> blob)
  {
    var reader = new VarIntReader(Open(Magic, blob.Span).Span);
    var strings = ReadStrings(ref reader);
    var files = new FileCoverage[reader.ReadCount(MinimumFileBytes)];
    string? previous = null;

    for (var i = 0; i < files.Length; i++)
    {
      var path = ReadReference(ref reader, strings);

      ThrowIfInvalidPath(ref reader, path, previous);
      files[i] = FileCoverage.Create(path, ReadLines(ref reader, strings));
      previous = path;
    }

    reader.ThrowIfNotAtEnd();

    return CoverageReport.Create(files);
  }

  /// <inheritdoc />
  public byte[] EncodeFileIndex(CoverageReport report) => FileIndexCodec.Encode(report);

  /// <inheritdoc />
  public IReadOnlyList<FileTotals> DecodeFileIndex(ReadOnlyMemory<byte> blob) => FileIndexCodec.Decode(blob.Span);

  #endregion
}
