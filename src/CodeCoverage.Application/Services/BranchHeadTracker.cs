// ------------------------------------------------------------------------------------------------
// <copyright file="BranchHeadTracker.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Finds the covered head of a branch: the complete commit nearest to its head (ARCHITECTURE §11.1).</summary>
/// <param name="commits">The repository of the commits.</param>
/// <param name="ancestry">The ancestry of the commits, read from the forge and cached.</param>
/// <remarks>
///   <para>
///     The search starts from the head and follows the first parents, over <see cref="MaxAncestors" /> ancestors at
///     most. The parents always come from the commits the forge describes, never from the order of the commits of a
///     push, and no date is compared: after a forced push the former head is no longer an ancestor, so it is no
///     longer found.
///   </para>
///   <para>Nothing is written here: the caller records the result on the branch, in its own save.</para>
/// </remarks>
internal sealed class BranchHeadTracker(ICommitRepository commits, CommitAncestry ancestry)
{
  #region Fields

  /// <summary>The number of ancestors of the head that are looked at, at most.</summary>
  internal const int MaxAncestors = 50;

  #endregion

  #region Methods

  /// <summary>Finds the covered head of a branch.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="repository">The address of the repository on its forge.</param>
  /// <param name="head">The head of the branch.</param>
  /// <param name="finalizing">
  ///   The commit whose finalization is being saved, or <see langword="null" />. Its stored row still shows the state
  ///   before the finalization, so <paramref name="outcome" /> is used for it instead.
  /// </param>
  /// <param name="outcome">The state <paramref name="finalizing" /> ends in, or <see langword="null" /> without one.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   The identifier of the nearest <see cref="CommitState.Complete" /> commit among the head and its ancestors, or
  ///   <see langword="null" /> when none is.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> or <paramref name="head" /> is <see langword="null" />.</exception>
  public async Task<long?> TrackAsync(
    long repositoryId,
    ForgeRepositoryRef repository,
    GitSha head,
    Commit? finalizing,
    CommitState? outcome,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(head);

    var chain = await ancestry.GetFirstParentChainAsync(repositoryId, repository, head, MaxAncestors, cancellationToken).ConfigureAwait(false);
    GitSha[] candidates = [head, .. chain.Select(static ancestor => ancestor.Sha)];
    var stored = await commits.ListByShasAsync(repositoryId, candidates, cancellationToken).ConfigureAwait(false);
    var bySha = stored.ToDictionary(static commit => commit.Sha);

    foreach (var sha in candidates)
    {
      if (finalizing is not null && sha == finalizing.Sha)
      {
        if (outcome is CommitState.Complete)
        {
          return finalizing.Id;
        }

        continue;
      }

      if (bySha.TryGetValue(sha, out var commit) && commit.State is CommitState.Complete)
      {
        return commit.Id;
      }
    }

    return null;
  }

  #endregion
}
