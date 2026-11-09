// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubActionsContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.RegularExpressions;

using CodeCoverage.Uploader.Serialization;

namespace CodeCoverage.Uploader.Ci;

/// <summary>Reads the context of a GitHub Actions run.</summary>
/// <remarks>
///   On the events of a pull request, <c>GITHUB_SHA</c> is the synthetic merge commit or the last
///   commit of the default branch: never the head. Only the event payload gives the head, which also
///   works with <c>fetch-depth: 1</c>; without it the commit is left unknown.
/// </remarks>
internal static partial class GitHubActionsContext
{
  #region Fields

  private const string Service = "github-actions";

  // Prefix of the branches of a merge queue: gh-readonly-queue/<branch>/pr-<number>-<sha>.
  private const string MergeQueuePrefix = "gh-readonly-queue/";

  #endregion

  #region Methods

  /// <summary>Reads the context.</summary>
  /// <param name="environment">Reads an environment variable; a blank one is <see langword="null" />.</param>
  /// <param name="readFile">Reads a text file.</param>
  /// <returns>The context.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static DetectedContext Read(
    Func<string, string?> environment,
    Func<string, string> readFile)
  {
    ArgumentNullException.ThrowIfNull(environment);
    ArgumentNullException.ThrowIfNull(readFile);

    return environment("GITHUB_EVENT_NAME") is "pull_request"
             or "pull_request_target"
             or "pull_request_review"
             or "pull_request_review_comment"
             ? ReadPullRequest(environment, readFile)
             : ReadBranchBuild(environment);
  }

  [GeneratedRegex(@"^refs/pull/(?<number>\d+)/merge$", RegexOptions.CultureInvariant)]
  private static partial Regex PullRequestRef();

  private static DetectedContext ReadPullRequest(
    Func<string, string?> environment,
    Func<string, string> readFile)
  {
    var pullRequest = ReadPayload(environment("GITHUB_EVENT_PATH"), readFile, out var failure);
    var head = Child(pullRequest, "head");
    var sha = DetectedContext.NormalizeSha(Text(Child(head, "sha")));

    return Common(environment) with
    {
      Sha = sha,
      Branch = environment("GITHUB_HEAD_REF") ?? Text(Child(head, "ref")),
      PullRequest = Number(Child(pullRequest, "number")) ?? NumberOfRef(environment("GITHUB_REF")),
      ShaFailure = sha is not null
                     ? null
                     : "The head of the pull request could not be read: "
                       + (failure ?? "the event payload has no valid pull_request.head.sha")
                       + ". GITHUB_SHA is not the head of a pull request, so it is not used.",
    };
  }

  private static DetectedContext ReadBranchBuild(Func<string, string?> environment)
  {
    var sha = DetectedContext.NormalizeSha(environment("GITHUB_SHA"));

    return Common(environment) with
    {
      Sha = sha,
      Branch = ReadBranch(environment),
      ShaFailure = sha is null ? "GITHUB_SHA is missing or is not a full commit SHA." : null,
    };
  }

  private static DetectedContext Common(Func<string, string?> environment)
  {
    var server = environment("GITHUB_SERVER_URL");
    var repository = environment("GITHUB_REPOSITORY");
    var runId = environment("GITHUB_RUN_ID");

    return new DetectedContext
    {
      Service = Service,
      Slug = repository,
      BuildUrl = server is null || repository is null || runId is null
                   ? null
                   : BuildUrl(server, repository, runId, environment("GITHUB_RUN_ATTEMPT")),
      BuildId = runId,
      JobId = environment("GITHUB_JOB"),
      Root = environment("GITHUB_WORKSPACE"),
      SlugFailure = "GITHUB_REPOSITORY is not set.",
    };
  }

  private static string BuildUrl(string server, string repository, string runId, string? attempt)
  {
    var run = $"{server.TrimEnd('/')}/{repository}/actions/runs/{runId}";

    return attempt is null ? run : $"{run}/attempts/{attempt}";
  }

  // GITHUB_REF_NAME is also set for a tag: a tag is not a branch.
  private static string? ReadBranch(Func<string, string?> environment)
  {
    var reference = environment("GITHUB_REF");

    if (reference is not null
        && !reference.StartsWith(DetectedContext.BranchRefPrefix, StringComparison.Ordinal))
    {
      return null;
    }

    var branch = environment("GITHUB_REF_NAME");

    return branch is not null && branch.StartsWith(MergeQueuePrefix, StringComparison.Ordinal)
             ? TargetOfMergeQueue(branch)
             : branch;
  }

  // The name of the target can hold slashes; the last segment is the one the queue generates.
  private static string TargetOfMergeQueue(string branch)
  {
    var target = branch[MergeQueuePrefix.Length..];
    var end = target.LastIndexOf('/');

    return end > 0 ? target[..end] : target;
  }

  private static JsonElement? ReadPayload(
    string? path,
    Func<string, string> readFile,
    out string? failure)
  {
    failure = null;

    if (path is null)
    {
      failure = "GITHUB_EVENT_PATH is not set";

      return null;
    }

    try
    {
      var payload = JsonSerializer.Deserialize(readFile(path), UploaderJsonContext.Default.JsonElement);

      return Child(payload, "pull_request");
    }
    catch (Exception exception) when (exception is IOException
                                        or UnauthorizedAccessException
                                        or ArgumentException
                                        or NotSupportedException
                                        or JsonException)
    {
      failure = $"the event payload is unreadable or malformed ({exception.GetType().Name})";

      return null;
    }
  }

  private static int? NumberOfRef(string? reference)
  {
    var match = PullRequestRef().Match(reference ?? string.Empty);

    return match.Success ? DetectedContext.ParseNumber(match.Groups["number"].Value) : null;
  }

  private static JsonElement? Child(JsonElement? parent, string name)
  {
    return parent is { ValueKind: JsonValueKind.Object } element
           && element.TryGetProperty(name, out var child)
             ? child
             : null;
  }

  private static string? Text(JsonElement? element)
  {
    return element is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
  }

  private static int? Number(JsonElement? element)
  {
    return element is { ValueKind: JsonValueKind.Number } value
           && value.TryGetInt32(out var number)
           && number > 0
             ? number
             : null;
  }

  #endregion
}
