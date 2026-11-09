// ------------------------------------------------------------------------------------------------
// <copyright file="CloverParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Reports.Parsers;

/// <summary>Reads the Clover reports, with the rules of Codecov (<c>clover.py</c>).</summary>
/// <remarks>
///   What Codecov refuses is refused here too: a report without name on a file, or without valid
///   number, type or condition counts on a line makes the parsing fail with a
///   <see cref="FormatException" />, and malformed XML with an <see cref="XmlException" />.
///   The <c>max_report_age</c> rule is not applied here, the parser receiving only the content of
///   the report. Each <c>file</c> element is loaded whole, the metrics that bound a PHP file coming
///   after its lines.
/// </remarks>
internal sealed class CloverParser : IReportParser
{
  #region Methods

  /// <summary>Reads a file, whose coverage is added to the one of its path.</summary>
  /// <param name="file">The file.</param>
  /// <param name="files">The lines read so far, by path.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <exception cref="FormatException">The file has no name, or a line is not valid.</exception>
  private static void ReadFile(
    XElement file,
    Dictionary<string, Dictionary<int, LineCoverage>> files,
    CancellationToken cancellationToken)
  {
    var path = GetPath(file);

    if (IsIgnored(path) || file.Element("line") is null)
    {
      return;
    }

    var endOfFile = GetEndOfFile(path, file);

    foreach (var line in file.Descendants("line"))
    {
      cancellationToken.ThrowIfCancellationRequested();

      var number = ParseLineNumber(line);

      if (number >= 1 && (endOfFile is 0 || number <= endOfFile))
      {
        AddLine(files, path, ReadLine(number, line));
      }
    }
  }

  /// <summary>Gets the path of a file.</summary>
  /// <param name="file">The file.</param>
  /// <returns>The <c>path</c> attribute unless it is empty, otherwise the <c>name</c> attribute.</returns>
  /// <exception cref="FormatException">The file has neither.</exception>
  private static string GetPath(XElement file)
  {
    var path = file.Attribute("path")?.Value;

    return string.IsNullOrEmpty(path)
      ? file.Attribute("name")?.Value ?? throw new FormatException("A file of the report has no name.")
      : path;
  }

  /// <summary>Tells whether Codecov leaves a path out: a template, or a PHP dependency.</summary>
  /// <param name="path">The path.</param>
  /// <returns><see langword="true" /> when the file must not be read.</returns>
  private static bool IsIgnored(string path)
  {
    return path.Length == 0
      || path.Contains('{', StringComparison.Ordinal)
      || (path.EndsWith(".php", StringComparison.Ordinal)
          && $"/{path}".Contains("/vendor/", StringComparison.Ordinal));
  }

  /// <summary>Gets the last line of a PHP file, whose reports go past the end of the source.</summary>
  /// <param name="path">The path of the file.</param>
  /// <param name="file">The file.</param>
  /// <returns>The first valid <c>loc</c> of its metrics; 0 when no limit applies.</returns>
  private static long GetEndOfFile(string path, XElement file)
  {
    if (!path.EndsWith(".php", StringComparison.Ordinal))
    {
      return 0;
    }

    foreach (var metrics in file.Descendants("metrics"))
    {
      if (long.TryParse(metrics.Attribute("loc")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lines))
      {
        return lines;
      }
    }

    return 0;
  }

  /// <summary>Reads the number of a line.</summary>
  /// <param name="line">The line.</param>
  /// <returns>The number.</returns>
  /// <exception cref="FormatException">The number is missing or is not valid.</exception>
  private static int ParseLineNumber(XElement line)
  {
    return int.TryParse(line.Attribute("num")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
      ? number
      : throw new FormatException("A line of the report has no valid number.");
  }

  /// <summary>Reads a line.</summary>
  /// <param name="number">The number of the line.</param>
  /// <param name="line">The line.</param>
  /// <returns>The coverage of the line.</returns>
  /// <exception cref="FormatException">The line is not valid.</exception>
  private static LineCoverage ReadLine(int number, XElement line)
  {
    return line.Attribute("type")?.Value switch
    {
      null => throw new FormatException($"The line {number} of the report has no type."),
      "cond" => ReadCondition(number, line),
      "method" => new LineCoverage(number, ReadHits(number, line), 0, 0, true, null),
      var _ => new LineCoverage(number, ReadHits(number, line), 0, 0, false, null),
    };
  }

  /// <summary>Reads a line of type <c>cond</c>.</summary>
  /// <param name="number">The number of the line.</param>
  /// <param name="line">The line.</param>
  /// <returns>A line of two branches, as many being covered as the counts that are not 0.</returns>
  /// <exception cref="FormatException">A count of the condition is not valid.</exception>
  private static LineCoverage ReadCondition(int number, XElement line)
  {
    var covered = (ReadConditionCount(number, line, "truecount"), ReadConditionCount(number, line, "falsecount")) switch
    {
      (0, 0) => 0,
      (0, var _) or (var _, 0) => 1,
      var _ => 2,
    };

    // Codecov does not read the count of a condition: one that is not valid is no error.
    var hits = TryParseInteger(line.Attribute("count")?.Value, out var count) ? Math.Max(count, 0) : 0;

    return new LineCoverage(number, hits, covered, 2, false, null);
  }

  /// <summary>Reads a required count of a condition.</summary>
  /// <param name="number">The number of the line.</param>
  /// <param name="line">The line.</param>
  /// <param name="name">The name of the attribute.</param>
  /// <returns>The count.</returns>
  /// <exception cref="FormatException">The count is missing or is not valid.</exception>
  private static long ReadConditionCount(int number, XElement line, string name)
  {
    return TryParseInteger(line.Attribute(name)?.Value, out var count)
      ? count
      : throw new FormatException($"The line {number} of the report has no valid {name}.");
  }

  /// <summary>Reads the hits of a line, none when its <c>count</c> is missing or empty.</summary>
  /// <param name="number">The number of the line.</param>
  /// <param name="line">The line.</param>
  /// <returns>The hits, at least 0.</returns>
  /// <exception cref="FormatException">The count is not valid.</exception>
  private static long ReadHits(int number, XElement line)
  {
    var value = line.Attribute("count")?.Value;

    if (string.IsNullOrEmpty(value))
    {
      return 0;
    }

    return TryParseInteger(value, out var count)
      ? Math.Max(count, 0)
      : throw new FormatException($"The line {number} of the report has no valid count.");
  }

  /// <summary>Reads an integer, which Python reads whatever its size.</summary>
  /// <param name="value">The attribute.</param>
  /// <param name="number">The number, kept within the range of <see cref="long" />.</param>
  /// <returns><see langword="true" /> when the attribute is an integer.</returns>
  private static bool TryParseInteger(string? value, out long number)
  {
    var isValid = BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);

    number = (long)BigInteger.Clamp(parsed, long.MinValue, long.MaxValue);

    return isValid;
  }

  /// <summary>Adds a line to its file, merging it with the value the file already has.</summary>
  /// <param name="files">The lines read so far, by path.</param>
  /// <param name="path">The path of the file.</param>
  /// <param name="line">The line.</param>
  private static void AddLine(
    Dictionary<string, Dictionary<int, LineCoverage>> files,
    string path,
    LineCoverage line)
  {
    if (!files.TryGetValue(path, out var lines))
    {
      lines = [];
      files[path] = lines;
    }

    lines[line.Line] = lines.TryGetValue(line.Line, out var existing)
      ? ReportMerger.MergeLine(existing, line)
      : line;
  }

  #endregion

  #region Implementation of IReportParser

  /// <inheritdoc />
  public ReportFormat Format => ReportFormat.Clover;

  /// <inheritdoc />
  public async Task<ParsedReport> ParseAsync(Stream report, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(report);

    using var reader = SafeXmlReaderFactory.Create(report);
    Dictionary<string, Dictionary<int, LineCoverage>> files = new(StringComparer.Ordinal);

    // Reading an element moves past its end: the next node must not be consumed by another read.
    while (!reader.EOF)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "file" })
      {
        var file = (XElement)await XNode.ReadFromAsync(reader, cancellationToken).ConfigureAwait(false);

        ReadFile(file, files, cancellationToken);
      }
      else
      {
        await reader.ReadAsync().ConfigureAwait(false);
      }
    }

    ParsedFile[] parsed =
    [
      .. files.OrderBy(file => file.Key, StringComparer.Ordinal)
              .Select(file => new ParsedFile(file.Key, [.. file.Value.Values.OrderBy(line => line.Line)])),
    ];

    return new ParsedReport(ReportFormat.Clover, [], parsed);
  }

  #endregion
}
