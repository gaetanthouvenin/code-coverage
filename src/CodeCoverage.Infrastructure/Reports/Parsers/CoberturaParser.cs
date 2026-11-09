// ------------------------------------------------------------------------------------------------
// <copyright file="CoberturaParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Reports.Parsers;

/// <summary>Reads the Cobertura reports, with the rules of Codecov (<c>cobertura.py</c>).</summary>
/// <remarks>
///   What Codecov refuses is refused here too: a report without file name on a class, without
///   valid number on a line or without valid hits on a plain line makes the parsing fail with a
///   <see cref="FormatException" />, and malformed XML with an <see cref="XmlException" />.
///   The <c>max_report_age</c> rule and the <c>parsers.cobertura</c> options are not applied here,
///   the parser receiving only the content of the report. Difference with Codecov: only the lines
///   of a class count, whereas Codecov also reads those of its <c>methods</c> element; the two
///   agree on the reports of coverlet and ReportGenerator, not on those of Istanbul, whose function
///   declarations are only listed there.
/// </remarks>
internal sealed partial class CoberturaParser : IReportParser
{
  #region Fields

  /// <summary>
  ///   The number of branches of a line up to which the uncovered ones are given an identifier: a
  ///   report claims this number, and an upload must not make the worker allocate whatever it says.
  /// </summary>
  private const int MaxNamedBranches = 4096;

  #endregion

  #region Methods

  /// <summary>Gets the expression of the count of a condition coverage, <c>50% (1/2)</c>.</summary>
  /// <returns>The expression.</returns>
  [GeneratedRegex(@"\((\d+)/(\d+)\)", RegexOptions.CultureInvariant)]
  private static partial Regex ConditionCount();

  /// <summary>Reads a class, whose coverage is added to the one of its file.</summary>
  /// <param name="reader">The reader, on the class; on return, on the node after it.</param>
  /// <param name="files">The lines read so far, by path.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>A task that completes when the class is read.</returns>
  /// <exception cref="FormatException">The class has no file name, or a line is not valid.</exception>
  private static async Task ReadClassAsync(
    XmlReader reader,
    Dictionary<string, Dictionary<int, LineCoverage>> files,
    CancellationToken cancellationToken)
  {
    var path = reader.GetAttribute("filename") ?? throw new FormatException("A class of the report has no filename.");

    if (path.Length == 0 || reader.IsEmptyElement)
    {
      await reader.SkipAsync().ConfigureAwait(false);

      return;
    }

    var depth = reader.Depth;

    await reader.ReadAsync().ConfigureAwait(false);

    while (reader.Depth > depth)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "methods" })
      {
        await reader.SkipAsync().ConfigureAwait(false);
      }
      else if (reader is { NodeType: XmlNodeType.Element, LocalName: "line" })
      {
        if (await ReadLineAsync(reader).ConfigureAwait(false) is { } line)
        {
          AddLine(files, path, line);
        }
      }
      else
      {
        await reader.ReadAsync().ConfigureAwait(false);
      }
    }

    await reader.ReadAsync().ConfigureAwait(false);
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

  /// <summary>Reads a line.</summary>
  /// <param name="reader">The reader, on the line; on return, on the node after it.</param>
  /// <returns>The line, or <see langword="null" /> when its number says it is not coverable.</returns>
  /// <exception cref="FormatException">The number of the line or its hits are not valid.</exception>
  private static async Task<LineCoverage?> ReadLineAsync(XmlReader reader)
  {
    var value = reader.GetAttribute("number") ?? throw new FormatException("A line of the report has no number.");
    var number = value is "undefined" ? 0 : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    if (number <= 0)
    {
      await reader.SkipAsync().ConfigureAwait(false);

      return null;
    }

    var hits = reader.GetAttribute("hits");
    var isBranch = string.Equals(reader.GetAttribute("branch"), "true", StringComparison.OrdinalIgnoreCase);
    var count = ConditionCount().Match(reader.GetAttribute("condition-coverage") ?? string.Empty);
    var missingBranches = reader.GetAttribute("missing-branches");
    var conditions = await ReadUncoveredConditionsAsync(reader).ConfigureAwait(false);

    if (!isBranch || !count.Success)
    {
      return TryParseHits(hits, out var lineHits)
        ? new LineCoverage(number, lineHits, 0, 0, false, null)
        : throw new FormatException($"The line {number} of the report has no valid hits.");
    }

    var covered = int.Parse(count.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
    var total = int.Parse(count.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);

    // Codecov classes 0/0 as a hit: here, a line without branch data must be executed to be one.
    if (total == 0)
    {
      return new LineCoverage(number, 1, 0, 0, false, null);
    }

    return new LineCoverage(
      number,
      TryParseHits(hits, out var branchHits) ? branchHits : 0,
      covered,
      total,
      false,
      NameMissingBranches(missingBranches, conditions, covered, total));
  }

  /// <summary>Reads the conditions of a line that are not fully covered.</summary>
  /// <param name="reader">The reader, on the line; on return, on the node after it.</param>
  /// <returns>The identifiers of the conditions, <c>number:type</c>.</returns>
  /// <exception cref="FormatException">A condition has no number or no type.</exception>
  private static async Task<List<string>> ReadUncoveredConditionsAsync(XmlReader reader)
  {
    List<string> conditions = [];
    var depth = reader.Depth;
    var isEmpty = reader.IsEmptyElement;

    await reader.ReadAsync().ConfigureAwait(false);

    if (isEmpty)
    {
      return conditions;
    }

    while (reader.Depth > depth)
    {
      if (reader is { NodeType: XmlNodeType.Element, LocalName: "condition" }
          && reader.GetAttribute("coverage") is not "100%")
      {
        conditions.Add($"{ReadConditionAttribute(reader, "number")}:{ReadConditionAttribute(reader, "type")}");
      }

      await reader.ReadAsync().ConfigureAwait(false);
    }

    await reader.ReadAsync().ConfigureAwait(false);

    return conditions;
  }

  /// <summary>Reads a required attribute of a condition.</summary>
  /// <param name="reader">The reader, on the condition.</param>
  /// <param name="name">The name of the attribute.</param>
  /// <returns>The value of the attribute.</returns>
  /// <exception cref="FormatException">The attribute is missing.</exception>
  private static string ReadConditionAttribute(XmlReader reader, string name) =>
    reader.GetAttribute(name) ?? throw new FormatException($"A condition of the report has no {name}.");

  /// <summary>Names the branches a line misses.</summary>
  /// <param name="attribute">The <c>missing-branches</c> attribute, which wins over the conditions.</param>
  /// <param name="conditions">The identifiers of the conditions that are not fully covered.</param>
  /// <param name="covered">The number of covered branches.</param>
  /// <param name="total">The number of branches.</param>
  /// <returns>The identifiers, or <see langword="null" /> when the report names none.</returns>
  private static string[]? NameMissingBranches(string? attribute, List<string> conditions, int covered, int total)
  {
    if (!string.IsNullOrEmpty(attribute))
    {
      var named = attribute.Split(',');

      return named.Length > 1 && Array.TrueForAll(named, branch => branch is "exit") ? ["loop", "exit"] : named;
    }

    // Some tools write one condition for a line that misses all its branches: the others are numbered.
    if (covered == 0 && total <= MaxNamedBranches && conditions.Count < total)
    {
      conditions.AddRange(
        Enumerable.Range(conditions.Count, total - conditions.Count)
                  .Select(branch => branch.ToString(CultureInfo.InvariantCulture)));
    }

    return conditions.Count > 0 ? [.. conditions] : null;
  }

  /// <summary>Reads hits, which Python reads as an integer or as a number with a fraction.</summary>
  /// <param name="value">The attribute.</param>
  /// <param name="hits">The hits, truncated and kept within 0 and <see cref="long.MaxValue" />.</param>
  /// <returns><see langword="true" /> when the attribute is a finite number.</returns>
  private static bool TryParseHits(string? value, out long hits)
  {
    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out hits))
    {
      hits = Math.Max(hits, 0);

      return true;
    }

    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) || !double.IsFinite(real))
    {
      return false;
    }

    hits = real >= long.MaxValue ? long.MaxValue : (long)Math.Max(real, 0);

    return true;
  }

  #endregion

  #region Implementation of IReportParser

  /// <inheritdoc />
  public ReportFormat Format => ReportFormat.Cobertura;

  /// <inheritdoc />
  public async Task<ParsedReport> ParseAsync(Stream report, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(report);

    using var reader = SafeXmlReaderFactory.Create(report);
    List<string> sourceRoots = [];
    Dictionary<string, Dictionary<int, LineCoverage>> files = new(StringComparer.Ordinal);

    // Reading an element moves past its end: the next node must not be consumed by another read.
    while (!reader.EOF)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "source" })
      {
        var root = await reader.ReadElementContentAsStringAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(root))
        {
          sourceRoots.Add(root);
        }
      }
      else if (reader is { NodeType: XmlNodeType.Element, LocalName: "class" })
      {
        await ReadClassAsync(reader, files, cancellationToken).ConfigureAwait(false);
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

    return new ParsedReport(ReportFormat.Cobertura, sourceRoots, parsed);
  }

  #endregion
}
