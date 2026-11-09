// ------------------------------------------------------------------------------------------------
// <copyright file="LcovParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Text;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Reports.Parsers;

/// <summary>Reads the LCOV tracefiles, with the rules of Codecov (<c>lcov.py</c>).</summary>
/// <remarks>
///   A tracefile is cut into records at each line starting with <c>end_of_record</c>, the last one
///   needing none; <see cref="LcovRecord" /> applies the rules of a record. As in Codecov, a
///   malformed line is skipped and never fails the parsing, which also holds for what Codecov
///   refuses by an exception, such as a negative line number. The stream is read through a window
///   of <see cref="MaxLineLength" /> characters: a longer line is dropped without being held in
///   memory. A byte order mark is honored, as in the detection of the format.
///   The <c>parsers.lcov.partials_as_hits</c> option is not applied here, the parser receiving only
///   the content of the report.
/// </remarks>
internal sealed class LcovParser : IReportParser
{
  #region Fields

  /// <summary>The text that closes a record, at the start of a line.</summary>
  private const string EndOfRecord = "end_of_record";

  /// <summary>
  ///   The number of characters from which a line is dropped: far beyond the longest paths and the
  ///   longest mangled names, and small enough for an upload not to make the worker hold a whole file.
  /// </summary>
  private const int MaxLineLength = 1024 * 1024;

  #endregion

  #region Methods

  /// <summary>Reads the lines of a report, as Codecov cuts them: on the line feed only.</summary>
  /// <param name="reader">The reader.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>The lines, without those of <see cref="MaxLineLength" /> characters or more.</returns>
  private static async IAsyncEnumerable<string> ReadLinesAsync(
    TextReader reader,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var window = new char[MaxLineLength];
    var length = 0;
    var isDropping = false;
    int read;

    while ((read = await reader.ReadAsync(window.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
    {
      var end = length + read;
      var start = 0;
      var feed = Array.IndexOf(window, '\n', length, read);

      while (feed >= 0)
      {
        if (!isDropping)
        {
          yield return new string(window, start, feed - start);
        }

        isDropping = false;
        start = feed + 1;
        feed = Array.IndexOf(window, '\n', start, end - start);
      }

      length = end - start;
      isDropping |= length == window.Length;

      if (isDropping)
      {
        length = 0;
      }
      else
      {
        Array.Copy(window, start, window, 0, length);
      }
    }

    if (!isDropping && length > 0)
    {
      yield return new string(window, 0, length);
    }
  }

  #endregion

  #region Implementation of IReportParser

  /// <inheritdoc />
  public ReportFormat Format => ReportFormat.Lcov;

  /// <inheritdoc />
  public async Task<ParsedReport> ParseAsync(Stream report, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(report);

    using var reader = new StreamReader(
      report,
      new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
      detectEncodingFromByteOrderMarks: true,
      leaveOpen: true);

    Dictionary<string, Dictionary<int, LineCoverage>> files = new(StringComparer.Ordinal);
    var record = new LcovRecord();

    await foreach (var line in ReadLinesAsync(reader, cancellationToken).ConfigureAwait(false))
    {
      if (line.StartsWith(EndOfRecord, StringComparison.Ordinal))
      {
        record.AddTo(files);
        record = new LcovRecord();
        record.Read(line[EndOfRecord.Length..]);
      }
      else
      {
        record.Read(line);
      }
    }

    record.AddTo(files);

    ParsedFile[] parsed =
    [
      .. files.OrderBy(file => file.Key, StringComparer.Ordinal)
              .Select(file => new ParsedFile(file.Key, [.. file.Value.Values.OrderBy(line => line.Line)])),
    ];

    return new ParsedReport(ReportFormat.Lcov, [], parsed);
  }

  #endregion
}
