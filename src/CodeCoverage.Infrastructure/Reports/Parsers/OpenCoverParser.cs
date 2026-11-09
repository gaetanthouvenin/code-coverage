// ------------------------------------------------------------------------------------------------
// <copyright file="OpenCoverParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Xml;
using System.Xml.Linq;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Reports.Parsers;

/// <summary>Reads the OpenCover reports, with the rules of Codecov (<c>csharp.py</c>).</summary>
/// <remarks>
///   What Codecov refuses is refused here too: a file without path or identifier, a reference to a
///   file without identifier, a sequence point without valid numbers or starting before line 1, and
///   a branch point without offset make the parsing fail with a <see cref="FormatException" />, and
///   malformed XML with an <see cref="XmlException" />. As in Codecov, the file table covers the
///   whole report, the last file declaring an identifier winning, and <c>skippedDueTo</c> is not
///   read. Unlike Codecov, the branch counts of a sequence point must be numbers, and the lines
///   added by multi-line sequence points are bounded by <see cref="MaxSpannedLines" />.
/// </remarks>
internal sealed class OpenCoverParser : IReportParser
{
  #region Fields

  /// <summary>
  ///   The number of lines the sequence points of a report may add beyond their first line: a report
  ///   claims its spans, and an upload must not make the worker expand whatever it says. Coverlet
  ///   writes single-line sequence points only.
  /// </summary>
  private const long MaxSpannedLines = 1_000_000;

  #endregion

  #region Methods

  /// <summary>Reads a file of the table.</summary>
  /// <param name="reader">The reader, on the file; it does not move.</param>
  /// <param name="paths">The paths read so far, by identifier.</param>
  /// <exception cref="FormatException">The file has no path, or a path without identifier.</exception>
  private static void ReadFile(XmlReader reader, Dictionary<string, string> paths)
  {
    var path = reader.GetAttribute("fullPath") ?? throw new FormatException("A file of the report has no fullPath.");

    if (path.Length > 0)
    {
      paths[reader.GetAttribute("uid") ?? throw new FormatException("A file of the report has no uid.")] = path;
    }
  }

  /// <summary>Adds the lines of a method to its file.</summary>
  /// <param name="method">The method.</param>
  /// <param name="files">The lines read so far, by identifier of file.</param>
  /// <param name="spannedLines">The lines the previous sequence points added beyond their first.</param>
  /// <returns>The lines added beyond their first, this method included.</returns>
  /// <exception cref="FormatException">A point of the method is not valid.</exception>
  private static long AddMethod(
    XElement method,
    Dictionary<string, Dictionary<int, LineCoverage>> files,
    long spannedLines)
  {
    // The methods without code have no file: they carry no sequence point either.
    if (method.Element("FileRef") is not { } fileRef)
    {
      return spannedLines;
    }

    var uid = ReadRequired(fileRef, "uid");

    if (!files.TryGetValue(uid, out var lines))
    {
      lines = [];
      files[uid] = lines;
    }

    var missingBranches = ReadMissingBranches(method);

    foreach (var point in method.Descendants("SequencePoint"))
    {
      spannedLines = AddSequencePoint(point, lines, missingBranches, spannedLines);
    }

    return spannedLines;
  }

  /// <summary>Reads the branches a method misses.</summary>
  /// <param name="method">The method.</param>
  /// <returns>The identifiers of the branches, <c>offset:offsetend</c>, by line.</returns>
  /// <exception cref="FormatException">A branch point is not valid.</exception>
  private static Dictionary<int, List<string>> ReadMissingBranches(XElement method)
  {
    Dictionary<int, List<string>> missing = [];

    foreach (var branch in method.Descendants("BranchPoint"))
    {
      if (ReadRequired(branch, "vc") is not "0" || branch.Attribute("sl") is not { } start)
      {
        continue;
      }

      var line = ReadNumber(start.Value, NumberStyles.Integer, "sl");
      var offset = ReadRequired(branch, "offset");
      var name = branch.Attribute("offsetend") is { } end ? $"{offset}:{end.Value}" : offset;

      if (!missing.TryGetValue(line, out var names))
      {
        names = [];
        missing[line] = names;
      }

      names.Add(name);
    }

    return missing;
  }

  /// <summary>Adds a sequence point to the lines it spans.</summary>
  /// <param name="point">The sequence point.</param>
  /// <param name="lines">The lines of its file.</param>
  /// <param name="missingBranches">The branches its method misses, by line.</param>
  /// <param name="spannedLines">The lines the previous sequence points added beyond their first.</param>
  /// <returns>The lines added beyond their first, this sequence point included.</returns>
  /// <exception cref="FormatException">The sequence point is not valid.</exception>
  private static long AddSequencePoint(
    XElement point,
    Dictionary<int, LineCoverage> lines,
    Dictionary<int, List<string>> missingBranches,
    long spannedLines)
  {
    var start = point.Attribute("sl")?.Value;
    var end = point.Attribute("el")?.Value;

    if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
    {
      return spannedLines;
    }

    var first = ReadNumber(start, NumberStyles.Integer, "sl");
    var last = ReadNumber(end, NumberStyles.Integer, "el");
    var hits = ReadHits(point.Attribute("vc")?.Value);
    var (covered, total) = ReadBranchCount(point);

    if (first < 1)
    {
      throw new FormatException($"A sequence point of the report starts at line {first}.");
    }

    var span = Math.Max(last - first, 0);

    spannedLines += span;

    if (spannedLines > MaxSpannedLines)
    {
      throw new FormatException("The sequence points of the report span too many lines.");
    }

    for (var offset = 0; offset <= span; offset++)
    {
      var number = first + offset;
      var line = total > 0
        ? new LineCoverage(number, hits, covered, total, false, missingBranches.GetValueOrDefault(number)?.ToArray())
        : new LineCoverage(number, hits, 0, 0, false, null);

      lines[number] = lines.TryGetValue(number, out var existing) ? ReportMerger.MergeLine(existing, line) : line;
    }

    return spannedLines;
  }

  /// <summary>Reads the branch counts of a sequence point.</summary>
  /// <param name="point">The sequence point.</param>
  /// <returns>The covered branches and the branches; 0 for both when the line has no branch.</returns>
  /// <exception cref="FormatException">The counts are not numbers.</exception>
  private static (int Covered, int Total) ReadBranchCount(XElement point)
  {
    var total = point.Attribute("bec")?.Value;

    return total is null or "0"
      ? (0, 0)
      : (ReadNumber(point.Attribute("bev")?.Value, NumberStyles.None, "bev"), ReadNumber(total, NumberStyles.None, "bec"));
  }

  /// <summary>Reads hits, which Python reads as an integer of any size.</summary>
  /// <param name="value">The attribute.</param>
  /// <returns>The hits, kept within 0 and <see cref="long.MaxValue" />.</returns>
  /// <exception cref="FormatException">The attribute is not an integer.</exception>
  private static long ReadHits(string? value)
  {
    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hits))
    {
      return Math.Max(hits, 0);
    }

    return double.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var real)
      ? real > 0 ? long.MaxValue : 0
      : throw new FormatException($"The hits '{value}' of the report are not valid.");
  }

  /// <summary>Reads a number that must be valid.</summary>
  /// <param name="value">The value of the attribute.</param>
  /// <param name="styles">The accepted forms.</param>
  /// <param name="name">The name of the attribute.</param>
  /// <returns>The number.</returns>
  /// <exception cref="FormatException">The value is not a number.</exception>
  private static int ReadNumber(string? value, NumberStyles styles, string name) =>
    int.TryParse(value, styles, CultureInfo.InvariantCulture, out var number)
      ? number
      : throw new FormatException($"The {name} '{value}' of the report is not valid.");

  /// <summary>Reads a required attribute.</summary>
  /// <param name="element">The element.</param>
  /// <param name="name">The name of the attribute.</param>
  /// <returns>The value of the attribute.</returns>
  /// <exception cref="FormatException">The attribute is missing.</exception>
  private static string ReadRequired(XElement element, string name) =>
    element.Attribute(name)?.Value ?? throw new FormatException($"A {element.Name.LocalName} of the report has no {name}.");

  /// <summary>Gives each file its lines, those of several identifiers of one path being merged.</summary>
  /// <param name="paths">The paths, by identifier.</param>
  /// <param name="linesByFile">The lines, by identifier of file.</param>
  /// <returns>The files, by path; an identifier without file is dropped.</returns>
  private static ParsedFile[] ResolveFiles(
    Dictionary<string, string> paths,
    Dictionary<string, Dictionary<int, LineCoverage>> linesByFile)
  {
    Dictionary<string, Dictionary<int, LineCoverage>> files = new(StringComparer.Ordinal);

    foreach (var (uid, lines) in linesByFile)
    {
      if (lines.Count == 0 || !paths.TryGetValue(uid, out var path))
      {
        continue;
      }

      if (!files.TryGetValue(path, out var merged))
      {
        files[path] = lines;

        continue;
      }

      foreach (var (number, line) in lines)
      {
        merged[number] = merged.TryGetValue(number, out var existing) ? ReportMerger.MergeLine(existing, line) : line;
      }
    }

    return
    [
      .. files.OrderBy(file => file.Key, StringComparer.Ordinal)
              .Select(file => new ParsedFile(file.Key, [.. file.Value.Values.OrderBy(line => line.Line)])),
    ];
  }

  #endregion

  #region Implementation of IReportParser

  /// <inheritdoc />
  public ReportFormat Format => ReportFormat.OpenCover;

  /// <inheritdoc />
  public async Task<ParsedReport> ParseAsync(Stream report, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(report);

    using var reader = SafeXmlReaderFactory.Create(report);
    Dictionary<string, string> paths = new(StringComparer.Ordinal);
    Dictionary<string, Dictionary<int, LineCoverage>> linesByFile = new(StringComparer.Ordinal);
    var spannedLines = 0L;

    // Reading a method moves past its end: the next node must not be consumed by another read.
    while (!reader.EOF)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "Method" })
      {
        var method = (XElement)await XNode.ReadFromAsync(reader, cancellationToken).ConfigureAwait(false);

        spannedLines = AddMethod(method, linesByFile, spannedLines);
      }
      else
      {
        if (reader is { NodeType: XmlNodeType.Element, LocalName: "File" })
        {
          ReadFile(reader, paths);
        }

        await reader.ReadAsync().ConfigureAwait(false);
      }
    }

    return new ParsedReport(ReportFormat.OpenCover, [], ResolveFiles(paths, linesByFile));
  }

  #endregion
}
