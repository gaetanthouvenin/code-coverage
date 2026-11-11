// ------------------------------------------------------------------------------------------------
// <copyright file="PushProcessor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>Applies a push to the branch it names (ARCHITECTURE §8.3).</summary>
/// <param name="branches">The repository of the branches.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="tracker">What finds the covered head of a branch.</param>
/// <param name="configs">What reads the configuration file of a commit.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     Applying the same push twice changes nothing more. The deliveries of a repository are handled one at a time,
///     but a job that failed runs again later, after deliveries that came after it: a push is trusted only when the
///     branch is where it started. Otherwise the forge says where the branch is now, so that a push replayed late
///     never moves the head back, nor brings back a branch deleted since.
///   </para>
///   <para>
///     The row of the branch is read before its covered head is computed: a finalization that saves the branch
///     meanwhile makes the save of the push a conflict, and the job runs again on what the finalization wrote.
///   </para>
/// </remarks>
internal sealed class PushProcessor(
  IBranchRepository branches,
  ICommitRepository commits,
  IRepositoryRepository repositories,
  BranchHeadTracker tracker,
  ConfigResolver configs,
  IForgeGatewayProvider gateways,
  IClock clock)
{
  #region Methods

  /// <summary>Applies a push: the head of its branch, its covered head, and the configuration of the default branch.</summary>
  /// <param name="repository">The repository, read in this scope.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="push">What the delivery says.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the push is applied.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public async Task ApplyAsync(
    TrackedRepository repository,
    ForgeRepositoryRef address,
    GitHubPushEvent push,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(address);
    ArgumentNullException.ThrowIfNull(push);

    // A tag, or a name no branch of the model can hold.
    if (push.Branch is not { Length: > 0 and <= Branch.NameMaxLength } name)
    {
      return;
    }

    if (push.Deleted)
    {
      await branches.RemoveAsync(repository.Id, name, cancellationToken).ConfigureAwait(false);

      return;
    }

    if (push.Head is null)
    {
      return;
    }

    if (await MoveHeadAsync(repository, address, name, push, cancellationToken).ConfigureAwait(false) is not { } head)
    {
      return;
    }

    if (push.TouchesConfig && string.Equals(name, repository.DefaultBranch, StringComparison.Ordinal))
    {
      await RefreshDefaultBranchYamlAsync(repository, address, head, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Moves the head of a branch, created when it is new, and records its covered head.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="name">The name of the branch.</param>
  /// <param name="push">What the delivery says; its head is set.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The head the branch was moved to, or <see langword="null" /> when the branch no longer exists.</returns>
  private async Task<GitSha?> MoveHeadAsync(
    TrackedRepository repository,
    ForgeRepositoryRef address,
    string name,
    GitHubPushEvent push,
    CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;

    // Read first: its version refuses the save of this push if a finalization saves the branch meanwhile.
    var branch = await branches.FindAsync(repository.Id, name, cancellationToken).ConfigureAwait(false);
    var head = push.Head!;

    if (branch?.HeadSha != push.Before)
    {
      // Not where the push started: a later push was applied first, or one was missed. The forge says where it is.
      if (await gateways.GetGateway(address.Forge).GetBranchHeadAsync(address, name, cancellationToken).ConfigureAwait(false) is not { } current)
      {
        return null;
      }

      head = current;
    }

    // The head is known before any upload names it.
    await commits.GetOrCreateAsync(repository.Id, head, now, cancellationToken).ConfigureAwait(false);

    var covered = await tracker.TrackAsync(repository.Id, address, head, null, null, cancellationToken).ConfigureAwait(false);

    if (branch is null)
    {
      branch = Branch.Create(repository.Id, name, head, now);
      branch.Cover(covered, now);

      // A finalization that created the row meanwhile makes this a conflict: the job, run again, finds it.
      await branches.AddAsync(branch, cancellationToken).ConfigureAwait(false);

      return head;
    }

    branch.MoveHead(head, now);
    branch.Cover(covered, now);
    await branches.UpdateAsync(branch, cancellationToken).ConfigureAwait(false);

    return head;
  }

  /// <summary>Reads the configuration file of the head of the default branch and records it on the repository.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="address">The address of the repository on its forge.</param>
  /// <param name="head">The head of the default branch.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the repository is saved, or when the head holds no file.</returns>
  /// <remarks>
  ///   The content always comes from the forge: the delivery only tells that a file may have changed. A head without
  ///   a file leaves the recorded configuration as it is.
  /// </remarks>
  private async Task RefreshDefaultBranchYamlAsync(
    TrackedRepository repository,
    ForgeRepositoryRef address,
    GitSha head,
    CancellationToken cancellationToken)
  {
    if (await configs.ReadFileAtCommitAsync(address, head, cancellationToken).ConfigureAwait(false) is not { } file)
    {
      return;
    }

    var version = repository.Version;

    repository.RefreshDefaultBranchYaml(file.Yaml, head, clock.UtcNow);

    if (repository.Version != version)
    {
      await repositories.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
    }
  }

  #endregion
}
