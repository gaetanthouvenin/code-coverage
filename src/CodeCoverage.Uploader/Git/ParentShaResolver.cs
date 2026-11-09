// ------------------------------------------------------------------------------------------------
// <copyright file="ParentShaResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Ci;

namespace CodeCoverage.Uploader.Git;

/// <summary>Finds the parent of the commit, only when the agent can tell it without ambiguity.</summary>
/// <remarks>The parent of a shallow clone is not there to read, and a merge has more than one.</remarks>
internal static class ParentShaResolver
{
  #region Methods

  /// <summary>Resolves the parent of HEAD.</summary>
  /// <param name="git">Runs git; returns its output, or <see langword="null" /> on a failure.</param>
  /// <returns>The parent in lower case, or <see langword="null" /> when it cannot be told.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="git" /> is <see langword="null" />.</exception>
  public static string? Resolve(Func<string[], string?> git)
  {
    ArgumentNullException.ThrowIfNull(git);

    if (git(["rev-parse", "--is-shallow-repository"]) is not "false")
    {
      return null;
    }

    var parents = GitRunner.ParentsOfHead(git);

    return parents.Count == 1 ? DetectedContext.NormalizeSha(parents[0]) : null;
  }

  #endregion
}
