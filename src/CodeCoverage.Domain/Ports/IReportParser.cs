// ------------------------------------------------------------------------------------------------
// <copyright file="IReportParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads the coverage reports of one format.</summary>
public interface IReportParser
{
  #region Properties

  /// <summary>Gets the format this parser reads.</summary>
  ReportFormat Format { get; }

  #endregion

  #region Methods

  /// <summary>Reads a report, in a streaming way.</summary>
  /// <param name="report">The content of the report; it stays open.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>The content of the report.</returns>
  Task<ParsedReport> ParseAsync(Stream report, CancellationToken cancellationToken);

  #endregion
}
