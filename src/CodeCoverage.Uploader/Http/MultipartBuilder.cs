// ------------------------------------------------------------------------------------------------
// <copyright file="MultipartBuilder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace CodeCoverage.Uploader.Http;

/// <summary>Builds the body of an upload (§5.1): the text fields first, then the list of files, then the reports.</summary>
internal static class MultipartBuilder
{
  #region Methods

  /// <summary>Builds a body; a new one for each attempt, its streams being read once.</summary>
  /// <param name="request">What the upload sends.</param>
  /// <param name="boundary">The boundary, or <see langword="null" /> for a random one.</param>
  /// <returns>The content.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  public static MultipartFormDataContent Build(UploadRequest request, string? boundary = null)
  {
    ArgumentNullException.ThrowIfNull(request);

    var content = boundary is null ? new MultipartFormDataContent() : new MultipartFormDataContent(boundary);

    Field(content, "slug", request.Slug);
    Field(content, "sha", request.Sha);
    Field(content, "branch", request.Branch);
    Field(content, "pr", request.PullRequest?.ToString(CultureInfo.InvariantCulture));
    Field(content, "parentSha", request.ParentSha);
    Field(content, "flags", request.Flags.Count == 0 ? null : string.Join(',', request.Flags));
    Field(content, "name", request.Name);
    Field(content, "ciService", request.CiService);
    Field(content, "buildUrl", request.BuildUrl);
    Field(content, "buildId", request.BuildId);
    Field(content, "jobId", request.JobId);
    Field(content, "root", request.Root);
    Field(content, "uploaderVersion", request.UploaderVersion);

    if (request.Network is not null)
    {
      var network = new StringContent(string.Join('\n', request.Network), Encoding.UTF8);

      network.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
      content.Add(network, "network", "network.txt");
    }

    foreach (var report in request.Reports)
    {
      content.Add(new GzipFileContent(report), "file", ReportName(report, request.Root));
    }

    return content;
  }

  /// <summary>Adds a text field, when it has a value.</summary>
  /// <param name="content">The body.</param>
  /// <param name="name">The name of the field.</param>
  /// <param name="value">Its value, or <see langword="null" />.</param>
  private static void Field(MultipartFormDataContent content, string name, string? value)
  {
    if (!string.IsNullOrWhiteSpace(value))
    {
      content.Add(new StringContent(value, Encoding.UTF8), name);
    }
  }

  /// <summary>Gives the name a report is sent under: relative to the root of the repository when it lies inside it.</summary>
  /// <param name="report">The full path of the report.</param>
  /// <param name="root">The root of the repository, or <see langword="null" />.</param>
  /// <returns>The name, written with <c>/</c>.</returns>
  private static string ReportName(string report, string? root)
  {
    if (root is null)
    {
      return Path.GetFileName(report);
    }

    var relative = Path.GetRelativePath(root, report);

    return (relative.StartsWith("..", StringComparison.Ordinal) ? Path.GetFileName(report) : relative).Replace('\\', '/');
  }

  #endregion
}
