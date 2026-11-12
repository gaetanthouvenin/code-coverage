// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Application.Features.PullRequests;

/// <summary>
///   The refusals of the pull requests, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the
///   message of each.
/// </summary>
internal static class PullRequestGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when a repository has no pull request of the number asked.</summary>
    /// <param name="pullRequest">The pull request that was looked for.</param>
    /// <exception cref="NotFoundException"><paramref name="pullRequest" /> is <see langword="null" />.</exception>
    public static void ThrowIfPullRequestNotFound([NotNull] PullRequest? pullRequest)
    {
      if (pullRequest is null)
      {
        throw new NotFoundException(PullRequestErrors.NotFound, "The pull request does not exist.");
      }
    }
  }

  #endregion
}
