// ------------------------------------------------------------------------------------------------
// <copyright file="CiContextDetector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Ci;

/// <summary>Finds out which build the uploader runs in (ARCHITECTURE §5.3-2).</summary>
/// <remarks>
///   The options win over the detection. Nothing here fails the build: what cannot be detected is
///   reported in <see cref="DetectedContext.Warnings" />.
/// </remarks>
internal static class CiContextDetector
{
  #region Methods

  /// <summary>Detects the context of the build.</summary>
  /// <param name="settings">The options of the run.</param>
  /// <param name="environment">Reads an environment variable.</param>
  /// <param name="readFile">Reads a text file; used for the event payload of GitHub Actions.</param>
  /// <param name="git">Runs git; returns its output, or <see langword="null" /> on a failure.</param>
  /// <returns>The context, with its warnings.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static DetectedContext Detect(
    UploaderSettings settings,
    Func<string, string?> environment,
    Func<string, string> readFile,
    Func<string[], string?> git)
  {
    ArgumentNullException.ThrowIfNull(settings);
    ArgumentNullException.ThrowIfNull(environment);
    ArgumentNullException.ThrowIfNull(readFile);
    ArgumentNullException.ThrowIfNull(git);

    // CI systems pass a missing value as an empty string: a blank variable counts as unset.
    string? Variable(string name) => environment(name)?.Trim() is { Length: > 0 } value ? value : null;

    DetectedContext detected;

    if (IsTrue(Variable("GITHUB_ACTIONS")))
    {
      detected = GitHubActionsContext.Read(Variable, readFile);
    }
    else if (IsTrue(Variable("TF_BUILD")))
    {
      detected = AzurePipelinesContext.Read(Variable);
    }
    else
    {
      detected = LocalContext.Read(Variable, git);
    }

    return ApplyOptions(detected, settings);
  }

  private static DetectedContext ApplyOptions(DetectedContext detected, UploaderSettings settings)
  {
    var warnings = new List<string>();
    var slug = settings.Slug ?? detected.Slug;

    if (slug is null)
    {
      warnings.Add($"{detected.SlugFailure} Pass --slug.");
    }

    return detected with
    {
      Slug = slug,
      Sha = ResolveSha(detected, settings.Sha, warnings),
      Branch = DetectedContext.StripBranchRef(settings.Branch) ?? detected.Branch,
      PullRequest = settings.PullRequest ?? detected.PullRequest,
      Root = settings.Root ?? detected.Root,
      ShaFailure = null,
      SlugFailure = null,
      Warnings = warnings,
    };
  }

  // A malformed --sha is not replaced by the detected commit: the upload was meant for another one.
  private static string? ResolveSha(
    DetectedContext detected,
    string? explicitSha,
    List<string> warnings)
  {
    if (explicitSha is not null)
    {
      var sha = DetectedContext.NormalizeSha(explicitSha.Trim());

      if (sha is null)
      {
        warnings.Add($"--sha is not a full commit SHA of 40 hexadecimal characters: '{explicitSha}'.");
      }

      return sha;
    }

    if (detected.Sha is null)
    {
      warnings.Add($"{detected.ShaFailure} Pass --sha.");
    }

    return detected.Sha;
  }

  private static bool IsTrue(string? value)
  {
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
  }

  #endregion
}
