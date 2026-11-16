// ------------------------------------------------------------------------------------------------
// <copyright file="JacocoParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Xml;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Reports.Parsers;

/// <summary>Reads the JaCoCo reports, with the rules of Codecov (<c>jacoco.py</c>).</summary>
/// <remarks>
///   <para>
///     What Codecov refuses is refused here too: a report without name on a package, a class or a
///     source file, or without valid counters on a line, makes the parsing fail with a
///     <see cref="FormatException" />, and malformed XML with an <see cref="XmlException" />. Its
///     DOCTYPE is skipped without being fetched. The <c>max_report_age</c> rule and the
///     <c>parsers.jacoco</c> options are not applied here, the parser receiving only the content of
///     the report.
///   </para>
///   <para>
///     JaCoCo writes paths relative to the source directory: where Codecov tries the name of the
///     report as a module directory, the same two directories are given as source roots, for the
///     <c>PathFixer</c> to try.
///   </para>
///   <para>
///     Each <c>method</c> with a line, of any class, nested ones included, is also a method of the
///     source file its class names in <c>sourcefilename</c> (ADR-016): executed once when its
///     <c>METHOD</c> counter has <c>covered</c> above 0, otherwise never, JaCoCo counting no
///     executions. The method lines Codecov marks (<see cref="LineCoverage.IsMethod" />) do not change.
///   </para>
/// </remarks>
internal sealed class JacocoParser : IReportParser
{
  #region Fields

  /// <summary>The source directory of a module that Codecov tries, below the name of the report.</summary>
  private const string ModuleSourceDirectory = "src/main/java";

  #endregion

  #region Methods

  /// <summary>Lists the directories the paths of the report may be relative to.</summary>
  /// <param name="reportName">The name of the report, which JaCoCo takes from the project.</param>
  /// <returns>The directories; none when the name is a title and not the one of a directory.</returns>
  private static string[] ListSourceRoots(string? reportName)
  {
    var project = reportName is null || reportName.Contains(' ') ? string.Empty : reportName.Trim('/');

    return project.Length == 0 ? [] : [project, $"{project}/{ModuleSourceDirectory}"];
  }

  /// <summary>Reads a package, whose coverage is added to the one of its files.</summary>
  /// <param name="reader">The reader, on the package; on return, on the node after it.</param>
  /// <param name="files">The lines read so far, by path.</param>
  /// <param name="methods">The methods read so far, by path.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>A task that completes when the package is read.</returns>
  /// <exception cref="FormatException">The package or one of its elements is not valid.</exception>
  private static async Task ReadPackageAsync(
    XmlReader reader,
    Dictionary<string, Dictionary<int, LineCoverage>> files,
    Dictionary<string, List<MethodCoverage>> methods,
    CancellationToken cancellationToken)
  {
    var packageName = ReadRequiredAttribute(reader, "name");
    var depth = reader.Depth;
    var isEmpty = reader.IsEmptyElement;

    await reader.ReadAsync().ConfigureAwait(false);

    if (isEmpty)
    {
      return;
    }

    Dictionary<string, HashSet<int>> methodLines = new(StringComparer.Ordinal);
    Dictionary<string, List<MethodCoverage>> sourceMethods = new(StringComparer.Ordinal);
    List<(string Name, List<LineCoverage> Lines)> sourceFiles = [];

    // The classes and the source files come in any order: the lines are only added once all are read.
    while (reader.Depth > depth)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "class" })
      {
        await ReadClassAsync(reader, methodLines, sourceMethods, cancellationToken).ConfigureAwait(false);
      }
      else if (reader is { NodeType: XmlNodeType.Element, LocalName: "sourcefile" })
      {
        var name = ReadRequiredAttribute(reader, "name");

        sourceFiles.Add((name, await ReadSourceFileLinesAsync(reader, cancellationToken).ConfigureAwait(false)));
      }
      else
      {
        await reader.ReadAsync().ConfigureAwait(false);
      }
    }

    await reader.ReadAsync().ConfigureAwait(false);

    foreach (var (name, lines) in sourceFiles)
    {
      AddSourceFile(files, methods, packageName, name, lines, methodLines, sourceMethods.GetValueOrDefault(name));
    }
  }

  /// <summary>
  ///   Reads the methods of a class, and the lines where the methods of a top-level class start.
  /// </summary>
  /// <param name="reader">The reader, on the class; on return, on the node after it.</param>
  /// <param name="methodLines">The lines Codecov marks as methods, by class name.</param>
  /// <param name="sourceMethods">The methods of the package, by name of source file.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>A task that completes when the class is read.</returns>
  /// <exception cref="FormatException">The class or one of its methods is not valid.</exception>
  private static async Task ReadClassAsync(
    XmlReader reader,
    Dictionary<string, HashSet<int>> methodLines,
    Dictionary<string, List<MethodCoverage>> sourceMethods,
    CancellationToken cancellationToken)
  {
    var name = ReadRequiredAttribute(reader, "name");
    var sourceName = reader.GetAttribute("sourcefilename");

    if (reader.IsEmptyElement)
    {
      await reader.SkipAsync().ConfigureAwait(false);

      return;
    }

    // Codecov only marks the method lines of a top-level class.
    HashSet<int>? lines = null;

    if (!name.Contains('$') && !methodLines.TryGetValue(name, out lines))
    {
      lines = [];
      methodLines[name] = lines;
    }

    List<MethodCoverage>? methods = null;

    if (sourceName is not null && !sourceMethods.TryGetValue(sourceName, out methods))
    {
      methods = [];
      sourceMethods[sourceName] = methods;
    }

    var depth = reader.Depth;

    await reader.ReadAsync().ConfigureAwait(false);

    while (reader.Depth > depth)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "method" })
      {
        if (await ReadMethodAsync(reader).ConfigureAwait(false) is var (line, hasComplexity, isCovered))
        {
          if (hasComplexity)
          {
            lines?.Add(line);
          }

          methods?.Add(new MethodCoverage(line, isCovered ? 1 : 0));
        }
      }
      else
      {
        await reader.ReadAsync().ConfigureAwait(false);
      }
    }

    await reader.ReadAsync().ConfigureAwait(false);
  }

  /// <summary>Reads a method: its line, its complexity counter and its method counter.</summary>
  /// <param name="reader">The reader, on the method; on return, on the node after it.</param>
  /// <returns>
  ///   The line; whether the method has a complexity counter, without which Codecov does not mark
  ///   its line; and whether its <c>METHOD</c> counter says it is covered. <see langword="null" />
  ///   when the method has no line.
  /// </returns>
  /// <exception cref="FormatException">The method is not valid.</exception>
  private static async Task<(int Line, bool HasComplexity, bool IsCovered)?> ReadMethodAsync(XmlReader reader)
  {
    var value = reader.GetAttribute("line");
    var line = value is null ? 0 : ParseInteger(value, "line");

    if (line <= 0 || reader.IsEmptyElement)
    {
      await reader.SkipAsync().ConfigureAwait(false);

      return null;
    }

    var depth = reader.Depth;
    var hasComplexity = false;
    var isCovered = false;

    await reader.ReadAsync().ConfigureAwait(false);

    while (reader.Depth > depth)
    {
      if (reader is { NodeType: XmlNodeType.Element, LocalName: "counter" })
      {
        var type = ReadRequiredAttribute(reader, "type");

        if (!hasComplexity && type is "COMPLEXITY")
        {
          _ = ParseInteger(ReadRequiredAttribute(reader, "missed"), "missed");
          _ = ParseInteger(ReadRequiredAttribute(reader, "covered"), "covered");
          hasComplexity = true;
        }
        else if (type is "METHOD")
        {
          // Codecov does not read this counter: one that is not valid is no error, and says not covered.
          isCovered = int.TryParse(reader.GetAttribute("covered"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var covered)
            && covered > 0;
        }
      }

      await reader.ReadAsync().ConfigureAwait(false);
    }

    await reader.ReadAsync().ConfigureAwait(false);

    return (line, hasComplexity, isCovered);
  }

  /// <summary>Reads the lines of a source file.</summary>
  /// <param name="reader">The reader, on the source file; on return, on the node after it.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>The lines, in the order of the report.</returns>
  /// <exception cref="FormatException">A line is not valid.</exception>
  private static async Task<List<LineCoverage>> ReadSourceFileLinesAsync(
    XmlReader reader,
    CancellationToken cancellationToken)
  {
    List<LineCoverage> lines = [];
    var depth = reader.Depth;
    var isEmpty = reader.IsEmptyElement;

    await reader.ReadAsync().ConfigureAwait(false);

    if (isEmpty)
    {
      return lines;
    }

    while (reader.Depth > depth)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "line" } && ReadLine(reader) is { } line)
      {
        lines.Add(line);
      }

      await reader.ReadAsync().ConfigureAwait(false);
    }

    await reader.ReadAsync().ConfigureAwait(false);

    return lines;
  }

  /// <summary>Reads a line, without moving the reader.</summary>
  /// <param name="reader">The reader, on the line.</param>
  /// <returns>The line, or <see langword="null" /> when its number says it is not coverable.</returns>
  /// <exception cref="FormatException">The number or the counters of the line are not valid.</exception>
  private static LineCoverage? ReadLine(XmlReader reader)
  {
    var missed = ReadBranchCount(reader, "mb");
    var covered = ReadBranchCount(reader, "cb");
    var total = (long)missed + covered;

    if (total > int.MaxValue)
    {
      throw new FormatException("A line of the report has too many branches.");
    }

    var hasHits = long.TryParse(reader.GetAttribute("ci"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hits);

    // As in Codecov, the instructions of a line are only required when it has no branch.
    if (!hasHits && total == 0)
    {
      throw new FormatException("A line of the report has no valid ci.");
    }

    var number = ParseInteger(ReadRequiredAttribute(reader, "nr"), "nr");

    return number > 0 ? new LineCoverage(number, Math.Max(hits, 0), covered, (int)total, false, null) : null;
  }

  /// <summary>Reads a number of branches.</summary>
  /// <param name="reader">The reader, on the line.</param>
  /// <param name="name">The name of the attribute.</param>
  /// <returns>The number of branches.</returns>
  /// <exception cref="FormatException">The attribute is missing or not a valid count.</exception>
  private static int ReadBranchCount(XmlReader reader, string name)
  {
    var count = ParseInteger(ReadRequiredAttribute(reader, name), name);

    return count >= 0 ? count : throw new FormatException($"A line of the report has a negative {name}.");
  }

  /// <summary>Reads a required attribute.</summary>
  /// <param name="reader">The reader, on the element.</param>
  /// <param name="name">The name of the attribute.</param>
  /// <returns>The value of the attribute.</returns>
  /// <exception cref="FormatException">The attribute is missing.</exception>
  private static string ReadRequiredAttribute(XmlReader reader, string name) =>
    reader.GetAttribute(name) ?? throw new FormatException($"A {reader.LocalName} of the report has no {name}.");

  /// <summary>Reads an integer the way Python does, with an invariant culture.</summary>
  /// <param name="value">The value.</param>
  /// <param name="name">The name of the attribute, for the error.</param>
  /// <returns>The integer.</returns>
  /// <exception cref="FormatException">The value is not an integer.</exception>
  private static int ParseInteger(string value, string name) =>
    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
      ? number
      : throw new FormatException($"The attribute {name} of the report is not a valid integer.");

  /// <summary>Adds the lines of a source file, flagging those that declare a method, and its methods.</summary>
  /// <param name="files">The lines read so far, by path.</param>
  /// <param name="methods">The methods read so far, by path.</param>
  /// <param name="packageName">The name of the package.</param>
  /// <param name="name">The name of the source file.</param>
  /// <param name="lines">The lines of the source file.</param>
  /// <param name="methodLines">The lines Codecov marks as methods, by class name.</param>
  /// <param name="sourceMethods">The methods of the source file, or <see langword="null" /> when none.</param>
  private static void AddSourceFile(
    Dictionary<string, Dictionary<int, LineCoverage>> files,
    Dictionary<string, List<MethodCoverage>> methods,
    string packageName,
    string name,
    List<LineCoverage> lines,
    Dictionary<string, HashSet<int>> methodLines,
    List<MethodCoverage>? sourceMethods)
  {
    var path = packageName.Length == 0 ? name : $"{packageName}/{name}";
    var isMethodLine = methodLines.GetValueOrDefault(GetClassName(packageName, name));

    if (!methods.TryGetValue(path, out var pathMethods))
    {
      pathMethods = [];
      methods[path] = pathMethods;
    }

    pathMethods.AddRange(sourceMethods ?? []);

    foreach (var line in lines)
    {
      AddLine(files, path, isMethodLine?.Contains(line.Line) is true ? line with { IsMethod = true } : line);
    }
  }

  /// <summary>Gets the name of the class whose methods Codecov looks at for a source file.</summary>
  /// <param name="packageName">The name of the package.</param>
  /// <param name="name">The name of the source file.</param>
  /// <returns>
  ///   The class of the same name: the ones of a default package, which keep their leading slash
  ///   here as in Codecov, and of another name, such as <c>FooKt</c>, match no class.
  /// </returns>
  private static string GetClassName(string packageName, string name)
  {
    var sourceName = $"{packageName}/{name}";
    var dot = sourceName.IndexOf('.', StringComparison.Ordinal);

    return dot < 0 ? sourceName : sourceName[..dot];
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
  public ReportFormat Format => ReportFormat.Jacoco;

  /// <inheritdoc />
  public async Task<ParsedReport> ParseAsync(Stream report, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(report);

    using var reader = SafeXmlReaderFactory.Create(report);

    await reader.MoveToContentAsync().ConfigureAwait(false);

    var sourceRoots = ListSourceRoots(reader.GetAttribute("name"));
    Dictionary<string, Dictionary<int, LineCoverage>> files = new(StringComparer.Ordinal);
    Dictionary<string, List<MethodCoverage>> methods = new(StringComparer.Ordinal);

    // Reading an element moves past its end: the next node must not be consumed by another read.
    while (!reader.EOF)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (reader is { NodeType: XmlNodeType.Element, LocalName: "package" })
      {
        await ReadPackageAsync(reader, files, methods, cancellationToken).ConfigureAwait(false);
      }
      else
      {
        await reader.ReadAsync().ConfigureAwait(false);
      }
    }

    ParsedFile[] parsed =
    [
      .. files.OrderBy(file => file.Key, StringComparer.Ordinal)
              .Select(
                file => new ParsedFile(
                  file.Key,
                  [.. file.Value.Values.OrderBy(line => line.Line)],
                  [.. methods[file.Key].OrderBy(method => method.Line)])),
    ];

    return new ParsedReport(ReportFormat.Jacoco, sourceRoots, parsed);
  }

  #endregion
}
