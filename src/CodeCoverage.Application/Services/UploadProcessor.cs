// ------------------------------------------------------------------------------------------------
// <copyright file="UploadProcessor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Turns the parts stored for an upload into its coverage (ARCHITECTURE §6.2, steps 4 to 7).</summary>
/// <param name="rawStore">The store of the received parts.</param>
/// <param name="catalog">The parsers of the reports.</param>
/// <param name="gate">The limit of the parsings running in the process.</param>
/// <param name="coverageOptions">The coverage settings.</param>
internal sealed class UploadProcessor(
  IRawUploadStore rawStore,
  IReportParserCatalog catalog,
  ParseGate gate,
  IOptionsMonitor<CoverageOptions> coverageOptions)
{
  #region Methods

  /// <summary>Processes an upload.</summary>
  /// <param name="upload">The upload.</param>
  /// <param name="config">The effective configuration of its commit.</param>
  /// <param name="readTree">Reads the files of the repository, when the upload did not send its list.</param>
  /// <param name="log">The log so far.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>What the processing gave.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public async Task<UploadProcessingResult> ProcessAsync(
    Upload upload,
    CoverageConfig config,
    Func<CancellationToken, Task<ForgeTree?>> readTree,
    ProcessingLog log,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(upload);
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(readTree);
    ArgumentNullException.ThrowIfNull(log);

    var parsed = new List<(string FileName, ParsedReport Report)>();
    var reportCount = 0;
    IReadOnlyList<string>? network = null;

    await foreach (var part in rawStore.OpenReadAsync(upload.PublicId, cancellationToken).ConfigureAwait(false))
    {
      if (part.Kind is UploadPartKind.Network)
      {
        network = await ReadLinesAsync(part.Content, cancellationToken).ConfigureAwait(false);

        continue;
      }

      reportCount++;

      try
      {
        var report = await ParseAsync(part, cancellationToken).ConfigureAwait(false);

        if (report is null)
        {
          log = log.Add($"{UploadErrors.UnsupportedFormat}: {part.FileName}");
        }
        else
        {
          parsed.Add((part.FileName, PartialsAsHitsTransform.Apply(report, config)));
        }
      }
      catch (Exception exception) when (IsParsingFailure(exception))
      {
        return new UploadProcessingResult(null, reportCount, 0, 0, log, UploadErrors.ParseFailed, $"The report {part.FileName} could not be read: {exception.GetType().Name}.");
      }
    }

    NetworkIndex? index;

    (index, log) = await BuildIndexAsync(network, readTree, log, cancellationToken).ConfigureAwait(false);

    return Resolve(upload, config, parsed, reportCount, index, log);
  }

  /// <summary>Tells whether an exception raised while a report is read comes from its content.</summary>
  /// <param name="exception">The exception.</param>
  /// <returns>
  ///   <see langword="false" /> for a cancellation and for a failure of the store the parts are read from (a database
  ///   or a timeout): those go to the retries of the job, and only the content of a report fails the upload for good.
  /// </returns>
  /// <remarks>
  ///   The cause is looked for in the whole chain of inner exceptions: without <c>EnableRetryOnFailure</c>, the default
  ///   execution strategy of both providers wraps a transient failure in an <see cref="InvalidOperationException" />.
  /// </remarks>
  private static bool IsParsingFailure(Exception exception)
  {
    if (exception is OperationCanceledException)
    {
      return false;
    }

    for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
    {
      if (cause is DbException or TimeoutException)
      {
        return false;
      }
    }

    return true;
  }

  /// <summary>Reads the lines of a part.</summary>
  /// <param name="content">The part.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The lines that are not blank.</returns>
  private static async Task<IReadOnlyList<string>> ReadLinesAsync(Stream content, CancellationToken cancellationToken)
  {
    using var reader = new StreamReader(content, leaveOpen: true);
    var lines = new List<string>();

    while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
    {
      if (!string.IsNullOrWhiteSpace(line))
      {
        lines.Add(line.Trim());
      }
    }

    return lines;
  }

  /// <summary>Gives the list of the files of the repository, and what it reports about it.</summary>
  /// <param name="network">The list sent by the uploader, or <see langword="null" />; an empty one counts as none.</param>
  /// <param name="readTree">Reads the tree of the repository.</param>
  /// <param name="log">The log so far.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The index, or <see langword="null" /> without any list, and the log.</returns>
  private static async Task<(NetworkIndex? Index, ProcessingLog Log)> BuildIndexAsync(
    IReadOnlyList<string>? network,
    Func<CancellationToken, Task<ForgeTree?>> readTree,
    ProcessingLog log,
    CancellationToken cancellationToken)
  {
    // An empty list is no list, as for PathFixer: git failed on the agent.
    if (network is { Count: > 0 })
    {
      return (new NetworkIndex(network), log);
    }

    var tree = await readTree(cancellationToken).ConfigureAwait(false);

    if (tree is null)
    {
      return (null, log.Add("no file list: paths not verified"));
    }

    var index = new NetworkIndex(tree.Entries.Where(static e => !e.IsDirectory).Select(static e => e.Path));

    return (index, tree.Truncated ? log.Add("the tree of the repository is truncated: some paths may not be resolved") : log);
  }

  /// <summary>Fixes the paths, then merges the files and applies <c>ignore</c>.</summary>
  /// <param name="upload">The upload.</param>
  /// <param name="config">The effective configuration.</param>
  /// <param name="parsed">The parsed reports.</param>
  /// <param name="reportCount">The number of reports.</param>
  /// <param name="index">The files of the repository, or <see langword="null" />.</param>
  /// <param name="log">The log so far.</param>
  /// <returns>What the processing gave.</returns>
  private UploadProcessingResult Resolve(
    Upload upload,
    CoverageConfig config,
    List<(string FileName, ParsedReport Report)> parsed,
    int reportCount,
    NetworkIndex? index,
    ProcessingLog log)
  {
    var fixes = FixRule.Parse(config.Fixes);

    foreach (var refused in fixes.RefusedFixes)
    {
      log = log.Add($"fix not applied, it cannot run without backtracking or does not compile: {refused}");
    }

    var flagPaths = new PathPatternSet(upload.Flags.SelectMany(flag => config.ResolveFlag(flag).Paths));
    var files = new List<FileCoverage>();
    var resolved = 0;
    var unresolved = 0;

    foreach (var (fileName, report) in parsed)
    {
      var fixer = new PathFixer(
        new PathFixContext
        {
          Fixes = fixes,
          Root = upload.RepoRoot,
          SourceRoots = report.SourceRoots,
          ReportPath = fileName,
          Network = index,
          DisableDefaultPathFixes = config.DisableDefaultPathFixes,
          FlagPaths = flagPaths,
          AllowFilenameOnlyMatch = coverageOptions.CurrentValue.AllowFilenameOnlyMatch,
        }
      );

      foreach (var file in report.Files)
      {
        var result = fixer.Fix(file.RawPath);

        if (result.Status is PathFixStatus.Unresolved or PathFixStatus.Ambiguous)
        {
          unresolved++;
          log = log.AddPath(file.RawPath);

          continue;
        }

        resolved++;
        files.Add(FileCoverage.Create(result.Path!, file.Lines));
      }
    }

    if (resolved == 0)
    {
      return new UploadProcessingResult(null, reportCount, 0, unresolved, log, UploadErrors.NoFileMapped, "No file of the reports was found in the repository.");
    }

    var filter = config.BuildPathFilter(upload.Flags);
    var merged = CoverageReport.Create(files.Where(file => filter.Includes(file.Path)));

    return new UploadProcessingResult(merged, reportCount, resolved, unresolved, log, null, null);
  }

  /// <summary>Parses a report.</summary>
  /// <param name="part">The report.</param>
  /// <param name="cancellationToken">A token to cancel the parsing.</param>
  /// <returns>Its content, or <see langword="null" /> when its format is not supported.</returns>
  private async Task<ParsedReport?> ParseAsync(UploadPart part, CancellationToken cancellationToken)
  {
    var head = new byte[IReportParserCatalog.HeadLength];
    var read = await part.Content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
    var format = catalog.Detect(head.AsMemory(0, read), part.FileName);

    if (format is not { } detected || catalog.Find(detected) is not { } parser)
    {
      return null;
    }

    using var place = await gate.EnterAsync(cancellationToken).ConfigureAwait(false);
    await using var stream = new HeadReplayStream(head, read, part.Content);

    return await parser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
