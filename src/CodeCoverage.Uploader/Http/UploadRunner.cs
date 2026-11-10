// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRunner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Ci;
using CodeCoverage.Uploader.Discovery;
using CodeCoverage.Uploader.Git;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Http;

/// <summary>Runs one upload: finds the reports, detects the context and sends everything (§5.3).</summary>
/// <param name="client">The sender of the upload.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="reporter">Prints the messages.</param>
internal sealed class UploadRunner(UploadClient client, Func<string, string?> environment, ConsoleReporter reporter)
{
  #region Methods

  /// <summary>Runs the upload.</summary>
  /// <param name="settings">The options of the run.</param>
  /// <param name="cancellationToken">A token to cancel the run.</param>
  /// <returns><see langword="true" /> when the server accepted the upload.</returns>
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

    if (settings.DryRun)
    {
      reporter.Info($"Dry run: nothing was sent to {settings.Url}.");

      return true;
    }

    return await client.SendAsync(settings.Url, settings.Token, request, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
