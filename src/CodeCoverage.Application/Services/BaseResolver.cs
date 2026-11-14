// ------------------------------------------------------------------------------------------------
// <copyright file="BaseResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>Finds what a commit is compared with: fork point, base report and the two diffs (ARCHITECTURE §7.4).</summary>
/// <param name="pullRequests">The repository of the pull requests.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="ancestry">The ancestry of a commit, read from the forge and cached.</param>
/// <param name="diffs">The diffs, read from the forge and cached.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="options">The processing options.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   Nothing comes from the git history of a CI agent: the pull request, the merge base, the parents and
///   the diffs are all read from the API of the forge, so a shallow clone gives the same result.
/// </remarks>
internal sealed class BaseResolver(
  IPullRequestRepository pullRequests,
  ICommitRepository commits,
  CommitAncestry ancestry,
  DiffProvider diffs,
  IForgeGatewayProvider gateways,
  IOptionsMonitor<ProcessingOptions> options,
  IClock clock)
{
  #region Fields

  private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

  #endregion

  #region Methods

  /// <summary>Resolves the base of a commit.</summary>
  /// <param name="repository">The repository of the commit.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="commit">The commit.</param>
  /// <param name="allowPseudoCompare">
  ///   Whether a report other than the one of the exact base may be used (<c>codecov.allow_pseudo_compare</c>).
  /// </param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The resolution.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="RetryLaterException">
  ///   The nearest ancestor with uploads is still being processed, and the wait for it has not run out.
  /// </exception>
  public async Task<BaseResolution> ResolveAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    bool allowPseudoCompare,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(commit);

    var address = repository.ToForgeRef(forge);
    var gateway = gateways.GetGateway(forge);
    var pull = await FindPullRequestAsync(repository.Id, address, gateway, commit, cancellationToken).ConfigureAwait(false);

    GitSha? forkPoint = null;
    int? behindBy = null;
    GitSha? start;

    if (pull is not null)
    {
      // One call gives the merge base and how far behind the commit is; the base of the pull is all that is left without it.
      var compared = await gateway.CompareAsync(address, pull.BaseShaGitHub, commit.Sha, cancellationToken).ConfigureAwait(false);

      forkPoint = compared?.MergeBaseSha ?? pull.BaseShaGitHub;
      behindBy = compared?.BehindBy;
      start = forkPoint;
    }
    else
    {
      var self = await ancestry.GetAsync(repository.Id, address, commit.Sha, cancellationToken).ConfigureAwait(false);

      start = self is { ParentShas.Count: > 0 } ? self.ParentShas[0] : null;
    }

    // Without pseudo compare, only the exact base counts: nothing further is read, nor waited for.
    var maxHops = allowPseudoCompare ? options.CurrentValue.BaseMaxAncestorWalk : 0;
    var (comparedTo, hops) = start is null
                               ? (null, 0)
                               : await FindNearestReportedAsync(repository.Id, address, commit, start, maxHops, cancellationToken).ConfigureAwait(false);

    DiffSet patchDiff;

    if (pull is not null && forkPoint is not null)
    {
      patchDiff = await GetPullPatchAsync(repository.Id, address, pull, forkPoint, commit.Sha, cancellationToken).ConfigureAwait(false);
    }
    else
    {
      patchDiff = comparedTo is null
                    ? BaseResolution.NoDiff()
                    : await GetCompareAsync(repository.Id, address, comparedTo.Sha, commit.Sha, cancellationToken).ConfigureAwait(false);
    }

    DiffSet comparisonDiff;

    if (comparedTo is null)
    {
      comparisonDiff = BaseResolution.NoDiff();
    }
    else if (pull is null || comparedTo.Sha == forkPoint)
    {
      comparisonDiff = patchDiff;
    }
    else
    {
      comparisonDiff = await GetCompareAsync(repository.Id, address, comparedTo.Sha, commit.Sha, cancellationToken).ConfigureAwait(false);
    }

    return new BaseResolution(pull, forkPoint, comparedTo, patchDiff, comparisonDiff, behindBy, comparedTo is null ? null : hops);
  }

  /// <summary>Finds the pull request of a commit: the open one whose head it is, else the one its uploads named.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="gateway">The gateway of the forge.</param>
  /// <param name="commit">The commit.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The pull request, or <see langword="null" /> for a push build.</returns>
  /// <remarks>The forge is the authority: the pull is always read from it, whether a stored row or an upload named it.</remarks>
  private async Task<PullRequest?> FindPullRequestAsync(
    long repositoryId,
    ForgeRepositoryRef address,
    IForgeGateway gateway,
    Commit commit,
    CancellationToken cancellationToken)
  {
    var open = await pullRequests.FindOpenByHeadShaAsync(repositoryId, commit.Sha, cancellationToken).ConfigureAwait(false);

    if ((open?.Number ?? commit.PullNumber) is not { } number)
    {
      return null;
    }

    // The stored row follows the webhooks, which can be late or lost: its head is confirmed with the forge,
    // because the files of a pull are those of the head the forge has now, whatever the row says.
    var read = await gateway.GetPullAsync(address, number, cancellationToken).ConfigureAwait(false);

    return read is null ? open : await pullRequests.UpsertAsync(repositoryId, read, clock.UtcNow, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Walks the first parents from a commit to the nearest one whose report is complete.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="head">The commit being resolved, whose completion starts the wait.</param>
  /// <param name="start">The commit the walk starts at.</param>
  /// <param name="maxHops">How many parents may be walked beyond <paramref name="start" />.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The commit and how many parents were walked to reach it, or no commit.</returns>
  /// <exception cref="RetryLaterException">An ancestor is still being processed, and the wait for it has not run out.</exception>
  private async Task<(Commit? Commit, int Hops)> FindNearestReportedAsync(
    long repositoryId,
    ForgeRepositoryRef address,
    Commit head,
    GitSha start,
    int maxHops,
    CancellationToken cancellationToken)
  {
    var settings = options.CurrentValue;
    var current = start;

    for (var hops = 0; hops <= maxHops; hops++)
    {
      var candidate = await commits.FindByShaAsync(repositoryId, current, cancellationToken).ConfigureAwait(false);

      // An imported commit has totals and no session: as a base, it would give totals at zero. It is walked over.
      if (candidate is { State: CommitState.Complete, IsImported: false })
      {
        return (candidate, hops);
      }

      if (candidate is { State: CommitState.Pending })
      {
        var waitedSince = head.CompletedAt ?? head.UpdatedAt;
        var remaining = waitedSince + TimeSpan.FromMinutes(settings.BaseWaitMinutes) - clock.UtcNow;

        if (remaining > TimeSpan.Zero)
        {
          throw new RetryLaterException(
            remaining < RetryDelay ? remaining : RetryDelay,
            $"The base commit {current.Value} is still being processed."
          );
        }
      }

      if (hops == maxHops)
      {
        break;
      }

      var git = await ancestry.GetAsync(repositoryId, address, current, cancellationToken).ConfigureAwait(false);

      if (git is not { ParentShas.Count: > 0 })
      {
        break;
      }

      current = git.ParentShas[0];
    }

    return (null, 0);
  }

  /// <summary>Reads the diff of the patch of a pull request, for the commit being resolved.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="pull">The pull request.</param>
  /// <param name="forkPoint">The fork point.</param>
  /// <param name="sha">The commit being resolved.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The diff.</returns>
  /// <remarks>
  ///   The files of the pull describe its head only: a commit that is no longer the head is compared with
  ///   the fork point by its own SHA, so that its patch is never the one of a later push.
  /// </remarks>
  private async Task<DiffSet> GetPullPatchAsync(
    long repositoryId,
    ForgeRepositoryRef address,
    PullRequest pull,
    GitSha forkPoint,
    GitSha sha,
    CancellationToken cancellationToken)
  {
    if (sha == pull.HeadSha)
    {
      var files = await diffs.GetPullDiffAsync(repositoryId, address, pull.Number, forkPoint, sha, cancellationToken).ConfigureAwait(false);

      if (files is not null)
      {
        return files;
      }
    }

    return await GetCompareAsync(repositoryId, address, forkPoint, sha, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Reads the diff between two commits.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="baseSha">The base.</param>
  /// <param name="headSha">The head.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The diff, marked truncated when the forge could not give it.</returns>
  private async Task<DiffSet> GetCompareAsync(
    long repositoryId,
    ForgeRepositoryRef address,
    GitSha baseSha,
    GitSha headSha,
    CancellationToken cancellationToken) =>
    await diffs.GetCompareDiffAsync(repositoryId, address, baseSha, headSha, cancellationToken).ConfigureAwait(false)
    ?? BaseResolution.UnavailableDiff(DiffSource.Compare);

  #endregion
}