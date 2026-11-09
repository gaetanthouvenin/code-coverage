// ------------------------------------------------------------------------------------------------
// <copyright file="AzurePipelinesContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Ci;

/// <summary>Reads the context of an Azure Pipelines build.</summary>
/// <remarks>
///   On a pull request build <c>BUILD_SOURCEVERSION</c> is the synthetic merge commit, and
///   <c>SYSTEM_PULLREQUEST_SOURCECOMMITID</c>, the head, is not always set: without it the commit is
///   left unknown.
/// </remarks>
internal static class AzurePipelinesContext
{
  #region Fields

  private const string Service = "azure-pipelines";

  #endregion

  #region Methods

  /// <summary>Reads the context.</summary>
  /// <param name="environment">Reads an environment variable; a blank one is <see langword="null" />.</param>
  /// <returns>The context.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="environment" /> is <see langword="null" />.</exception>
  public static DetectedContext Read(Func<string, string?> environment)
  {
    ArgumentNullException.ThrowIfNull(environment);

    var provider = environment("BUILD_REPOSITORY_PROVIDER");
    var isGitHub = string.Equals(provider, "GitHub", StringComparison.OrdinalIgnoreCase);
    var pullRequestId = environment("SYSTEM_PULLREQUEST_PULLREQUESTID");
    var pullRequestNumber = environment("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER");
    var isPullRequest = pullRequestId is not null
                        || pullRequestNumber is not null
                        || string.Equals(
                          environment("BUILD_REASON"),
                          "PullRequest",
                          StringComparison.OrdinalIgnoreCase);
    var sha = DetectedContext.NormalizeSha(
      environment("SYSTEM_PULLREQUEST_SOURCECOMMITID")
      ?? (isPullRequest ? null : environment("BUILD_SOURCEVERSION")));
    var buildId = environment("BUILD_BUILDID");

    return new DetectedContext
    {
      Service = Service,
      Slug = isGitHub ? environment("BUILD_REPOSITORY_NAME") : null,
      Sha = sha,
      Branch = ReadBranch(environment, isPullRequest),
      PullRequest = DetectedContext.ParseNumber(pullRequestNumber)
                    ?? DetectedContext.ParseNumber(pullRequestId),
      BuildUrl = BuildUrl(environment, buildId),
      BuildId = buildId,
      JobId = environment("SYSTEM_JOBID"),
      Root = environment("BUILD_SOURCESDIRECTORY"),
      ShaFailure = sha is not null
                     ? null
                     : isPullRequest
                       ? "SYSTEM_PULLREQUEST_SOURCECOMMITID is missing or malformed. "
                         + "BUILD_SOURCEVERSION is the merge commit of a pull request build, so it is not used."
                       : "BUILD_SOURCEVERSION is missing or is not a full commit SHA.",
      SlugFailure = isGitHub
                      ? "BUILD_REPOSITORY_NAME is not set."
                      : $"BUILD_REPOSITORY_NAME gives an owner/repo slug only for a GitHub repository, not for '{provider ?? "an unknown provider"}'.",
    };
  }

  // Azure gives the source branch of a pull request as a ref for Azure Repos and as a name for
  // GitHub. BUILD_SOURCEBRANCH can also be a tag, a pull request merge ref or a TFVC path.
  private static string? ReadBranch(Func<string, string?> environment, bool isPullRequest)
  {
    if (isPullRequest)
    {
      return DetectedContext.StripBranchRef(environment("SYSTEM_PULLREQUEST_SOURCEBRANCH"));
    }

    var reference = environment("BUILD_SOURCEBRANCH");

    return reference is not null
           && reference.StartsWith(DetectedContext.BranchRefPrefix, StringComparison.Ordinal)
             ? DetectedContext.StripBranchRef(reference)
             : null;
  }

  private static string? BuildUrl(Func<string, string?> environment, string? buildId)
  {
    var collection = environment("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI");
    var project = environment("SYSTEM_TEAMPROJECT");

    return collection is null || project is null || buildId is null
             ? null
             : $"{collection.TrimEnd('/')}/{Uri.EscapeDataString(project)}/_build/results?buildId={buildId}";
  }

  #endregion
}
