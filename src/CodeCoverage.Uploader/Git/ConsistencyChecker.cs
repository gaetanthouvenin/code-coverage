// ------------------------------------------------------------------------------------------------
// <copyright file="ConsistencyChecker.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Ci;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Git;

/// <summary>Compares the detected commit with the commit that was checked out.</summary>
/// <remarks>It only reports: a difference never changes what is sent, and never breaks a build.</remarks>
internal static class ConsistencyChecker
{
  #region Methods

  /// <summary>Reports what does not match.</summary>
  /// <param name="context">What was detected.</param>
  /// <param name="git">Runs git; returns its output, or <see langword="null" /> on a failure.</param>
  /// <param name="reporter">Prints the messages.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static void Check(DetectedContext context, Func<string[], string?> git, ConsoleReporter reporter)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(git);
    ArgumentNullException.ThrowIfNull(reporter);

    var head = DetectedContext.NormalizeSha(git(["rev-parse", "HEAD"]));

    if (context.Sha is null || head is null || context.Sha == head)
    {
      return;
    }

    if (GitRunner.ParentsOfHead(git) is [_, var second] && DetectedContext.NormalizeSha(second) == context.Sha)
    {
      reporter.Info($"HEAD is a merge commit whose second parent is the detected commit {context.Sha}: the coverage is reported on that commit.");
    }
    else if (context.PullRequest is null)
    {
      reporter.Warning($"The detected commit {context.Sha} is not the commit that was checked out ({head}).");
    }
  }

  #endregion
}
