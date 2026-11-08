// ------------------------------------------------------------------------------------------------
// <copyright file="IReportParserCatalog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Ports;

/// <summary>Tells the format of a coverage report and finds the parser that reads it.</summary>
public interface IReportParserCatalog
{
  #region Fields

  /// <summary>The number of bytes, from the start of a report, that <see cref="Detect" /> looks at.</summary>
  const int HeadLength = 4096;

  #endregion

  #region Methods

  /// <summary>Tells the format of a report.</summary>
  /// <param name="head">The start of the report; what lies beyond <see cref="HeadLength" /> is ignored.</param>
  /// <param name="fileName">The name of the report, which settles a content that is not conclusive.</param>
  /// <returns>The format, or <see langword="null" /> when the report is in no supported format.</returns>
  ReportFormat? Detect(ReadOnlyMemory<byte> head, string fileName);

  /// <summary>Finds the parser of a format.</summary>
  /// <param name="format">The format.</param>
  /// <returns>The parser, or <see langword="null" /> when no parser reads this format.</returns>
  IReportParser? Find(ReportFormat format);

  #endregion
}
