// ------------------------------------------------------------------------------------------------
// <copyright file="GitRunner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CodeCoverage.Uploader.Git;

/// <summary>Runs git; a git that fails never throws, so it cannot break a build.</summary>
internal static class GitRunner
{
  #region Fields

  private const int TimeoutMilliseconds = 10_000;

  #endregion

  #region Methods

  /// <summary>Runs git and returns its output, trimmed.</summary>
  /// <param name="arguments">The arguments of git.</param>
  /// <param name="workingDirectory">The directory to run in; <see langword="null" /> for the current one.</param>
  /// <returns>The output, or <see langword="null" /> when git failed or timed out.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
  public static string? Run(string[] arguments, string? workingDirectory) => RunRaw(arguments, workingDirectory)?.Trim();

  /// <summary>Runs git and returns its output as it is, for an output that a separator structures.</summary>
  /// <param name="arguments">The arguments of git.</param>
  /// <param name="workingDirectory">The directory to run in; <see langword="null" /> for the current one.</param>
  /// <returns>The output, or <see langword="null" /> when git failed or timed out.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
  public static string? RunRaw(string[] arguments, string? workingDirectory)
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

    try
    {
      process.Start();

      var output = process.StandardOutput.ReadToEndAsync();

      // Read and dropped: a full pipe would block git.
      process.ErrorDataReceived += static (_, _) => { };
      process.BeginErrorReadLine();

      if (!process.WaitForExit(TimeoutMilliseconds))
      {
        process.Kill(entireProcessTree: true);

        return null;
      }

      return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
    }
    catch (Exception exception) when (exception is Win32Exception
                                        or InvalidOperationException
                                        or IOException
                                        or UnauthorizedAccessException)
    {
      return null;
    }
  }

  /// <summary>Reads the parents of HEAD.</summary>
  /// <param name="git">Runs git; returns its output, or <see langword="null" /> on a failure.</param>
  /// <returns>The parents, the first parent first; empty for a root commit or when git failed.</returns>
  public static IReadOnlyList<string> ParentsOfHead(Func<string[], string?> git)
  {
    ArgumentNullException.ThrowIfNull(git);

    return git(["rev-parse", "HEAD^@"])?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
  }

  #endregion
}
