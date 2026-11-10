// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRunner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Uploader.Ci;
using CodeCoverage.Uploader.Commands;
using CodeCoverage.Uploader.Discovery;
using CodeCoverage.Uploader.Git;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Http;

/// <summary>Runs one upload: finds the reports, detects the context and sends everything (§5.3).</summary>
/// <param name="client">The sender of the upload.</param>
/// <param name="poller">Waits for the processing of the upload, with <c>--wait</c>.</param>
/// <param name="finalizer">Declares the uploads of the commit complete, with <c>--finalize</c>.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="reporter">Prints the messages.</param>
internal sealed class UploadRunner(UploadClient client, UploadStatusPoller poller, FinalizeClient finalizer, Func<string, string?> environment, ConsoleReporter reporter)
{
  #region Methods

  /// <summary>Runs the upload.</summary>
  /// <param name="settings">The options of the run.</param>
  /// <param name="cancellationToken">A token to cancel the run.</param>
  /// <returns>
  ///   <see langword="true" /> when the server accepted the upload and, with <c>--wait</c>, processed it;
  ///   <see langword="true" /> as well for a dry run, which sends nothing.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="settings" /> is <see langword="null" />.</exception>
  public async Task<bool> RunAsync(UploaderSettings settings, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(settings);

    var workingDirectory = Environment.CurrentDirectory;
    var context = CiContextDetector.Detect(settings, environment, File.ReadAllText, arguments => GitRunner.Run(arguments, workingDirectory));
    var root = settings.Root ?? context.Root ?? workingDirectory;
    var reports = new ReportFinder(environment, workingDirectory, reporter).Find(root, settings.Files, settings.DisableSearch);

    if (reports.Count == 0)
    {
      return false;
    }

    foreach (var report in reports)
    {
      reporter.Info($"Found the report {report}");
    }

    if (context.Slug is null || context.Sha is null)
    {
      reporter.Error(context.SlugFailure ?? context.ShaFailure ?? "The repository or the commit could not be detected: pass --slug and --sha.");

      return false;
    }

    reporter.Detail(
      $"Context: {context.Service ?? "no CI service"}, slug {context.Slug}, sha {context.Sha}, branch {context.Branch ?? "(none)"}, "
      + $"pull request {context.PullRequest?.ToString(CultureInfo.InvariantCulture) ?? "(none)"}, root {root}"
    );

    string? Git(string[] arguments) => GitRunner.Run(arguments, root);

    ConsistencyChecker.Check(context, Git, reporter);

    var request = new UploadRequest
    {
      Slug = context.Slug,
      Sha = context.Sha,
      Branch = context.Branch,
      PullRequest = context.PullRequest,
      ParentSha = settings.ParentSha ?? ParentShaResolver.Resolve(Git),
      Flags = settings.Flags,
      Name = settings.Name,
      CiService = context.Service,
      BuildUrl = context.BuildUrl,
      BuildId = context.BuildId,
      JobId = context.JobId,
      Root = root,
      UploaderVersion = UploadClient.Version,
      Network = settings.NoNetwork ? null : NetworkCollector.Collect(arguments => GitRunner.RunRaw(arguments, root)),
      Reports = reports,
    };

    reporter.Detail(
      request.Network is null
        ? "The file list of the repository is not sent."
        : $"The file list of the repository holds {request.Network.Count.ToString(CultureInfo.InvariantCulture)} paths."
    );

    if (settings.DryRun)
    {
      DryRunPrinter.Print(reporter, settings, request, OidcTokenFetcher.IsAvailable(environment), SizeOf);

      return true;
    }

    var receipt = await client.SendAsync(settings.Url, settings.Token, request, cancellationToken).ConfigureAwait(false);

    if (receipt is null)
    {
      return false;
    }

    var succeeded = true;

    if (settings.Wait)
    {
      if (receipt.StatusUrl is null)
      {
        // The upload is accepted. No address is built in its place: the routes belong to the server.
        reporter.Warning("The server gave no address to follow the processing: --wait is ignored.");
      }
      else
      {
        succeeded = await poller.PollAsync(receipt.StatusUrl, receipt.Credential, cancellationToken).ConfigureAwait(false);
      }
    }

    // After the wait, and whatever it gave: the commit is notified with what the server could process.
    if (settings.Finalize)
    {
      succeeded &= await finalizer.CompleteAsync(settings.Url, context.Slug, context.Sha, receipt.Credential, cancellationToken).ConfigureAwait(false);
    }

    return succeeded;
  }

  /// <summary>Reads the size of a report.</summary>
  /// <param name="path">The path of the report.</param>
  /// <returns>Its size in bytes, or <see langword="null" /> when it cannot be read.</returns>
  private static long? SizeOf(string path)
  {
    try
    {
      return new FileInfo(path).Length;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return null;
    }
  }

  #endregion
}
