// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLogPrinter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Uploader.Http;

namespace CodeCoverage.Uploader.Output;

/// <summary>Prints what the server says of an upload whose processing ended.</summary>
internal static class ProcessingLogPrinter
{
  #region Fields

  /// <summary>The number of unresolved paths printed without <c>--verbose</c>.</summary>
  public const int MaxPaths = 10;

  #endregion

  #region Methods

  /// <summary>Prints the outcome of the processing, its counts and its log.</summary>
  /// <param name="reporter">Prints the messages.</param>
  /// <param name="status">The state of the upload, processed or failed.</param>
  /// <exception cref="ArgumentNullException"><paramref name="reporter" /> or <paramref name="status" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The line of the counts reads « resolved 412/415 files; unresolved: a, b » : the paths the server
  ///   kept, the first <see cref="MaxPaths" /> of them without <c>--verbose</c>.
  /// </remarks>
  public static void Print(ConsoleReporter reporter, UploadStatus status)
  {
    ArgumentNullException.ThrowIfNull(reporter);
    ArgumentNullException.ThrowIfNull(status);

    if (status.State is UploadStatus.Failed)
    {
      reporter.Error($"The processing of the upload failed: {status.Error ?? "the server gave no reason"}");
    }
    else
    {
      reporter.Info("The upload was processed.");
    }

    reporter.Info(Counts(status, reporter.IsVerbose));

    var messages = status.Log?.Messages ?? [];

    foreach (var message in messages)
    {
      reporter.Info($"  {message}");
    }

    var dropped = (status.Log?.MessageTotal ?? 0) - messages.Count;

    if (dropped > 0)
    {
      reporter.Info($"  (+{dropped.ToString(CultureInfo.InvariantCulture)} more messages)");
    }
  }

  /// <summary>Builds the line of the counts.</summary>
  /// <param name="status">The state of the upload.</param>
  /// <param name="all">Whether every path the server kept is printed.</param>
  /// <returns>The line.</returns>
  private static string Counts(UploadStatus status, bool all)
  {
    var resolved = status.ResolvedFiles.ToString(CultureInfo.InvariantCulture);
    var total = (status.ResolvedFiles + status.UnresolvedFiles).ToString(CultureInfo.InvariantCulture);
    var line = $"resolved {resolved}/{total} files";
    var paths = status.Log?.Paths ?? [];
    var pathTotal = Math.Max(status.Log?.PathTotal ?? 0, paths.Count);

    if (pathTotal == 0)
    {
      return line;
    }

    var shown = all ? paths : [.. paths.Take(MaxPaths)];
    var more = pathTotal - shown.Count;
    var names = string.Join(", ", shown);

    return more > 0
             ? $"{line}; unresolved: {names}{(shown.Count > 0 ? " " : string.Empty)}(+{more.ToString(CultureInfo.InvariantCulture)} more)"
             : $"{line}; unresolved: {names}";
  }

  #endregion
}
