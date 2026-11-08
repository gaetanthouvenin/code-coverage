// ------------------------------------------------------------------------------------------------
// <copyright file="ReportParserCatalog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using System.Xml;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Reports;

/// <summary>The parsers of the host, and the detection of the format of a report.</summary>
/// <remarks>
///   An XML report is recognized by its root element, so the markup of a comment never misleads the
///   detection; the root element <c>report</c> also covers the DOCTYPE of JaCoCo. The name of the
///   file only settles a content that looks like a report but is cut by the detection window: it
///   never makes a report of a content that does not look like one.
/// </remarks>
internal sealed class ReportParserCatalog : IReportParserCatalog
{
  #region Fields

  private readonly Dictionary<ReportFormat, IReportParser> _parsers;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ReportParserCatalog" /> class.</summary>
  /// <param name="parsers">The parsers, one per format at most.</param>
  public ReportParserCatalog(IEnumerable<IReportParser> parsers)
  {
    ArgumentNullException.ThrowIfNull(parsers);

    _parsers = parsers.ToDictionary(parser => parser.Format);
  }

  #endregion

  #region Methods

  /// <summary>Decodes the start of a report, in UTF-8 unless a byte order mark says otherwise.</summary>
  /// <param name="head">The bytes; a character cut at the end is replaced, which is harmless here.</param>
  /// <returns>The text, without byte order mark.</returns>
  private static string Decode(ReadOnlySpan<byte> head)
  {
    if (head.StartsWith(Encoding.UTF8.Preamble))
    {
      return Encoding.UTF8.GetString(head[Encoding.UTF8.Preamble.Length..]);
    }

    if (head.StartsWith(Encoding.Unicode.Preamble))
    {
      return Encoding.Unicode.GetString(head[Encoding.Unicode.Preamble.Length..]);
    }

    return head.StartsWith(Encoding.BigEndianUnicode.Preamble)
      ? Encoding.BigEndianUnicode.GetString(head[Encoding.BigEndianUnicode.Preamble.Length..])
      : Encoding.UTF8.GetString(head);
  }

  /// <summary>Removes the directories from the name of a report, written for any system.</summary>
  /// <param name="fileName">The name of the report, or its path.</param>
  /// <returns>The name of the file.</returns>
  private static string NameOf(string fileName) => fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];

  /// <summary>Tells the format of an XML report from its root element.</summary>
  /// <param name="text">The start of the report.</param>
  /// <param name="name">The name of the file.</param>
  /// <returns>The format, or <see langword="null" /> when none is recognized.</returns>
  private static ReportFormat? DetectXml(string text, string name)
  {
    using var reader = SafeXmlReaderFactory.Create(new StringReader(text));

    try
    {
      if (reader.MoveToContent() is not XmlNodeType.Element)
      {
        return null;
      }

      return reader.LocalName switch
      {
        "CoverageSession" => ReportFormat.OpenCover,
        "report" => ReportFormat.Jacoco,
        "coverage" => DetectCoverageRoot(reader, name),
        _ => null,
      };
    }
    catch (XmlException)
    {
      return null;
    }
  }

  /// <summary>Tells Clover from Cobertura, which share the root element <c>coverage</c>.</summary>
  /// <param name="reader">The reader, on the root element.</param>
  /// <param name="name">The name of the file.</param>
  /// <returns>The format, or <see langword="null" /> when none is recognized.</returns>
  private static ReportFormat? DetectCoverageRoot(XmlReader reader, string name)
  {
    if (reader.GetAttribute("clover") is not null)
    {
      return ReportFormat.Clover;
    }

    if (reader.GetAttribute("line-rate") is not null || reader.GetAttribute("lines-valid") is not null)
    {
      return ReportFormat.Cobertura;
    }

    return DetectFromChildren(reader) ?? DetectCoverageFromName(name);
  }

  /// <summary>Looks for the child element that tells Clover from Cobertura.</summary>
  /// <param name="reader">The reader, on the root element.</param>
  /// <returns>The format, or <see langword="null" /> when the visible children do not tell.</returns>
  private static ReportFormat? DetectFromChildren(XmlReader reader)
  {
    try
    {
      while (reader.Read())
      {
        if (reader is { NodeType: XmlNodeType.Element, Depth: 1, LocalName: "project" })
        {
          return ReportFormat.Clover;
        }

        if (reader is { NodeType: XmlNodeType.Element, Depth: 1, LocalName: "packages" })
        {
          return ReportFormat.Cobertura;
        }
      }
    }
    catch (XmlException)
    {
      // The detection window cuts the document: the children seen so far did not tell.
      return null;
    }

    return null;
  }

  /// <summary>Tells Clover from Cobertura by the name of the file.</summary>
  /// <param name="name">The name of the file.</param>
  /// <returns>The format, or <see langword="null" /> when the name does not tell.</returns>
  private static ReportFormat? DetectCoverageFromName(string name)
  {
    if (name.Contains("clover", StringComparison.OrdinalIgnoreCase))
    {
      return ReportFormat.Clover;
    }

    return name.Contains("cobertura", StringComparison.OrdinalIgnoreCase) ? ReportFormat.Cobertura : null;
  }

  /// <summary>Tells whether a text is the start of an LCOV tracefile.</summary>
  /// <param name="text">The start of the report.</param>
  /// <param name="name">The name of the file, needed when the first record outgrows the window.</param>
  /// <returns>LCOV, or <see langword="null" /> when the text is not a tracefile.</returns>
  private static ReportFormat? DetectLcov(string text, string name)
  {
    var opensFile = text.StartsWith("SF:", StringComparison.Ordinal) || text.Contains("\nSF:", StringComparison.Ordinal);

    return opensFile && (text.Contains("end_of_record", StringComparison.Ordinal) || HasLcovName(name))
      ? ReportFormat.Lcov
      : null;
  }

  /// <summary>Tells whether the name of a file is the one of a tracefile.</summary>
  /// <param name="name">The name of the file.</param>
  /// <returns><see langword="true" /> when the name is the one of a tracefile.</returns>
  private static bool HasLcovName(string name) =>
    name.Contains("lcov", StringComparison.OrdinalIgnoreCase)
    || name.EndsWith(".info", StringComparison.OrdinalIgnoreCase);

  #endregion

  #region Implementation of IReportParserCatalog

  /// <inheritdoc />
  public ReportFormat? Detect(ReadOnlyMemory<byte> head, string fileName)
  {
    ArgumentNullException.ThrowIfNull(fileName);

    var text = Decode(head.Span[..Math.Min(head.Length, IReportParserCatalog.HeadLength)]);
    var name = NameOf(fileName);

    return text.AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal)
      ? DetectXml(text, name)
      : DetectLcov(text, name);
  }

  /// <inheritdoc />
  public IReportParser? Find(ReportFormat format) => _parsers.GetValueOrDefault(format);

  #endregion
}
