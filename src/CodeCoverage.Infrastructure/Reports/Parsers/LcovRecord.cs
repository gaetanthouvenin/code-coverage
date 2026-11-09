// ------------------------------------------------------------------------------------------------
// <copyright file="LcovRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Infrastructure.Reports.Parsers;

/// <summary>One record of an LCOV tracefile, with the rules of <c>_process_file</c> of Codecov.</summary>
/// <remarks>
///   The branches of a line replace what its <c>DA</c> entries gave, even when none is taken. Lines
///   are keyed by their text until the end of the record, as Codecov compares them. As in Codecov, a
///   second <c>SF</c> in a record drops the lines of the first one, but not its branches.
/// </remarks>
internal sealed class LcovRecord
{
  #region Fields

  private readonly Dictionary<string, Dictionary<string, bool>> _branches = new(StringComparer.Ordinal);
  private readonly HashSet<string> _functionLines = new(StringComparer.Ordinal);
  private readonly HashSet<string> _manglingLines = new(StringComparer.Ordinal);
  private Dictionary<int, LineCoverage> _lines = [];
  private string? _path;
  private bool _isJavaScript;
  private bool _isCpp;
  private bool _isAbandoned;

  #endregion

  #region Methods

  /// <summary>Reads one line of the record; a malformed one is skipped.</summary>
  /// <param name="line">The line.</param>
  public void Read(string line)
  {
    ArgumentNullException.ThrowIfNull(line);

    var colon = line.IndexOf(':');

    if (_isAbandoned || colon < 0)
    {
      return;
    }

    var method = line[..colon];
    var content = line[(colon + 1)..].Trim();

    if (method is "TN" or "LF" or "LH" or "FNF" or "FNH" or "BRF" or "BRH" or "FNDA")
    {
      return;
    }

    if (method is "SF")
    {
      OpenFile(content);
    }
    else if (_path is null)
    {
      // Whatever comes before a file makes Codecov give up on the record.
      _isAbandoned = true;
    }
    else if (method is "DA")
    {
      ReadExecutionCount(content);
    }
    else if (method is "FN" && !_isJavaScript)
    {
      ReadFunction(content);
    }
    else if (method is "BRDA" && !_isJavaScript)
    {
      ReadBranch(content, _path);
    }
  }

  /// <summary>Adds the lines of the record to their file, merging them with what the file has.</summary>
  /// <param name="files">The lines read so far, by path.</param>
  public void AddTo(Dictionary<string, Dictionary<int, LineCoverage>> files)
  {
    ArgumentNullException.ThrowIfNull(files);

    if (_path is null)
    {
      return;
    }

    ApplyBranches();

    if (_lines.Count == 0)
    {
      return;
    }

    if (!files.TryGetValue(_path, out var lines))
    {
      lines = [];
      files[_path] = lines;
    }

    foreach (var (number, line) in _lines)
    {
      lines[number] = lines.TryGetValue(number, out var existing) ? ReportMerger.MergeLine(existing, line) : line;
    }
  }

  /// <summary>Starts the file of an <c>SF</c> entry.</summary>
  /// <param name="path">The path, which an empty entry does not give.</param>
  private void OpenFile(string path)
  {
    _path = path.Length > 0 ? path : null;
    _isJavaScript = path.EndsWith(".js", StringComparison.Ordinal);
    _isCpp = path.EndsWith(".cpp", StringComparison.Ordinal);
    _lines = [];
  }

  /// <summary>Reads a <c>DA</c> entry: <c>line,count[,checksum]</c>.</summary>
  /// <param name="content">The content of the entry.</param>
  private void ReadExecutionCount(string content)
  {
    var fields = content.Split(',', 3);

    if (fields.Length < 2
        || fields[0].StartsWith('0')
        || !TryParseLine(fields[0], out var number)
        || !TryParseHits(fields[1], out var hits))
    {
      return;
    }

    LineCoverage line = new(number, hits, 0, 0, false, null);

    _lines[number] = _lines.TryGetValue(number, out var existing) ? ReportMerger.MergeLine(existing, line) : line;
  }

  /// <summary>Reads an <c>FN</c> entry: <c>line,name</c>; it creates no line.</summary>
  /// <param name="content">The content of the entry.</param>
  private void ReadFunction(string content)
  {
    var fields = content.Split(',', 2);

    if (fields.Length < 2)
    {
      return;
    }

    if (_isCpp && (fields[1].StartsWith("_Z", StringComparison.Ordinal) || fields[1].StartsWith("_G", StringComparison.Ordinal)))
    {
      _manglingLines.Add(fields[0]);
    }
    else
    {
      _functionLines.Add(fields[0]);
    }
  }

  /// <summary>Reads a <c>BRDA</c> entry: <c>line,block,branch,taken</c>; the last one of a branch wins.</summary>
  /// <param name="content">The content of the entry.</param>
  /// <param name="path">The path of the file.</param>
  private void ReadBranch(string content, string path)
  {
    var fields = content.Split(',', 4);

    if (fields.Length < 4 || fields[0] is "0" or "" || (fields[0] is "1" && path.EndsWith(".ts", StringComparison.Ordinal)))
    {
      return;
    }

    if (!_branches.TryGetValue(fields[0], out var branches))
    {
      branches = new Dictionary<string, bool>(StringComparer.Ordinal);
      _branches[fields[0]] = branches;
    }

    branches[$"{fields[1]}:{fields[2]}"] = fields[3] is not ("-" or "0");
  }

  /// <summary>Replaces the lines that have branches by their branch coverage.</summary>
  private void ApplyBranches()
  {
    foreach (var (text, branches) in _branches)
    {
      if (_manglingLines.Contains(text) || !TryParseLine(text, out var number))
      {
        continue;
      }

      string[] missing = [.. branches.Where(branch => !branch.Value).Select(branch => branch.Key).Order(StringComparer.Ordinal)];

      _lines[number] = new LineCoverage(
        number,
        0,
        branches.Count - missing.Length,
        branches.Count,
        _functionLines.Contains(text),
        missing.Length > 0 ? missing : null);
    }
  }

  /// <summary>Reads a line number.</summary>
  /// <param name="value">The field.</param>
  /// <param name="number">The number.</param>
  /// <returns><see langword="true" /> when the field is a number from 1.</returns>
  private static bool TryParseLine(string value, out int number) =>
    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0;

  /// <summary>Reads hits, which Python reads as an integer or as a decimal number.</summary>
  /// <param name="value">The field.</param>
  /// <param name="hits">The hits, truncated and kept within 0 and <see cref="long.MaxValue" />.</param>
  /// <returns><see langword="true" /> when the field is a finite number.</returns>
  private static bool TryParseHits(string value, out long hits)
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
}
