// ------------------------------------------------------------------------------------------------
// <copyright file="DryRunPrinter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Commands;

/// <summary>Prints what an upload would send (<c>--dry-run</c>), without sending it.</summary>
internal static class DryRunPrinter
{
  #region Fields

  private const string None = "(none)";

  #endregion

  #region Methods

  /// <summary>Prints the request an upload would send.</summary>
  /// <param name="reporter">Prints the messages.</param>
  /// <param name="settings">The options of the run.</param>
  /// <param name="request">What the upload would send.</param>
  /// <param name="hasOidc">Whether GitHub Actions can be asked for a token.</param>
  /// <param name="fileSize">Reads the size of a report, in bytes, or <see langword="null" /> when it cannot be read.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <remarks>The credential is never printed: only whether there is one, and where it would come from.</remarks>
  public static void Print(ConsoleReporter reporter, UploaderSettings settings, UploadRequest request, bool hasOidc, Func<string, long?> fileSize)
  {
    ArgumentNullException.ThrowIfNull(reporter);
    ArgumentNullException.ThrowIfNull(settings);
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(fileSize);

    reporter.Info($"Dry run: nothing is sent to {settings.Url}. The upload would hold:");
    Field(reporter, "slug", request.Slug);
    Field(reporter, "sha", request.Sha);
    Field(reporter, "branch", request.Branch);
    Field(reporter, "pull request", request.PullRequest?.ToString(CultureInfo.InvariantCulture));
    Field(reporter, "parent sha", request.ParentSha);
    Field(reporter, "flags", request.Flags.Count == 0 ? null : string.Join(',', request.Flags));
    Field(reporter, "name", request.Name);
    Field(reporter, "ci service", request.CiService);
    Field(reporter, "build id", request.BuildId);
    Field(reporter, "job id", request.JobId);
    Field(reporter, "build url", request.BuildUrl);
    Field(reporter, "root", request.Root);
    Field(reporter, "uploader", request.UploaderVersion);
    Field(reporter, "credential", Credential(settings, hasOidc));
    Field(
      reporter,
      "file list",
      request.Network is null ? "not sent" : $"{request.Network.Count.ToString(CultureInfo.InvariantCulture)} paths of git ls-files"
    );

    reporter.Info($"  reports ({request.Reports.Count.ToString(CultureInfo.InvariantCulture)}), each sent as gzip:");

    foreach (var report in request.Reports)
    {
      var size = fileSize(report);

      reporter.Info(size is null ? $"    {report}" : $"    {report} ({size.Value.ToString(CultureInfo.InvariantCulture)} bytes)");
    }

    if (settings.Wait)
    {
      reporter.Info("  --wait: the processing would then be waited for.");
    }
  }

  private static string Credential(UploaderSettings settings, bool hasOidc)
  {
    if (settings.Token is not null)
    {
      return "an upload token (not printed)";
    }

    return hasOidc ? "none given: GitHub Actions would be asked for an OIDC token" : "none: the server would refuse the upload";
  }

  private static void Field(ConsoleReporter reporter, string name, string? value) => reporter.Info($"  {name}: {value ?? None}");

  #endregion
}
