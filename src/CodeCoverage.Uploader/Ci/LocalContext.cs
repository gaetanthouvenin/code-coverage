// ------------------------------------------------------------------------------------------------
// <copyright file="LocalContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeCoverage.Uploader.Ci;

/// <summary>Reads the context of a developer machine, or of a CI service without an adapter.</summary>
internal static partial class LocalContext
{
  #region Fields

  private const string LocalService = "local";
  private const string OtherService = "other";
  private const int GitTimeoutMilliseconds = 10_000;

  #endregion

  #region Methods

  /// <summary>Reads the context.</summary>
  /// <param name="environment">Reads an environment variable; a blank one is <see langword="null" />.</param>
  /// <param name="git">Runs git; returns its output, or <see langword="null" /> on a failure.</param>
  /// <returns>The context.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static DetectedContext Read(
    Func<string, string?> environment,
    Func<string[], string?> git)
  {
    ArgumentNullException.ThrowIfNull(environment);
    ArgumentNullException.ThrowIfNull(git);

    var sha = DetectedContext.NormalizeSha(git(["rev-parse", "HEAD"]));
    var branch = git(["branch", "--show-current"]);
    var slug = ParseSlug(git(["remote", "get-url", "origin"]));

    return new DetectedContext
    {
      Service = environment("CI") is "true" or "1" ? OtherService : LocalService,
      Slug = slug,
      Sha = sha,
      Branch = string.IsNullOrEmpty(branch) ? null : branch,
      ShaFailure = sha is null ? "git could not give the current commit." : null,
      SlugFailure = "No owner/repo slug could be read from the remote 'origin'.",
    };
  }

  /// <summary>Runs git and captures its output; never throws for a git that fails.</summary>
  /// <param name="arguments">The arguments of git.</param>
  /// <param name="workingDirectory">The directory to run in; <see langword="null" /> for the current one.</param>
  /// <returns>The trimmed output, or <see langword="null" /> when git failed or timed out.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
  public static string? RunGit(string[] arguments, string? workingDirectory)
  {
    ArgumentNullException.ThrowIfNull(arguments);

    var startInfo = new ProcessStartInfo("git")
    {
      WorkingDirectory = workingDirectory ?? string.Empty,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      StandardOutputEncoding = Encoding.UTF8,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    foreach (var argument in arguments)
    {
      startInfo.ArgumentList.Add(argument);
    }

    using var process = new Process { StartInfo = startInfo };
    var output = new StringBuilder();

    process.OutputDataReceived += (_, line) => output.AppendLine(line.Data);

    // Read and dropped: a full pipe would block git.
    process.ErrorDataReceived += static (_, _) => { };

    try
    {
      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      if (!process.WaitForExit(GitTimeoutMilliseconds))
      {
        process.Kill(entireProcessTree: true);

        return null;
      }

      // The overload with a timeout returns before the last output line is delivered.
      process.WaitForExit();

      return process.ExitCode == 0 ? output.ToString().Trim() : null;
    }
    catch (Exception exception) when (exception is Win32Exception
                                        or InvalidOperationException
                                        or IOException
                                        or UnauthorizedAccessException)
    {
      return null;
    }
  }

  // Exactly two segments, like the parse_slug of the Codecov CLI. The address is never printed: it
  // can hold a credential.
  private static string? ParseSlug(string? remote)
  {
    var match = RemoteAddress().Match(remote ?? string.Empty);

    return match.Success ? match.Groups["slug"].Value : null;
  }

  [GeneratedRegex(
    @"^(?:[a-zA-Z][a-zA-Z0-9+.-]*://)?(?:[^@/]+@)?[^/:.][^/:]*(?::\d+/|[:/])/*(?<slug>[^/\s:]+/[^/\s]+?)(?:\.git)?/?$",
    RegexOptions.CultureInvariant)]
  private static partial Regex RemoteAddress();

  #endregion
}
