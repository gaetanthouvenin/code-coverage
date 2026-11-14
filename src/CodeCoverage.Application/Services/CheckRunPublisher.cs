// ------------------------------------------------------------------------------------------------
// <copyright file="CheckRunPublisher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Publishes a status as a GitHub check run, with its annotations in batches of 50 (M5.05, ADR-007).</summary>
/// <param name="gitHub">The gateway of what only GitHub offers an App.</param>
/// <remarks>
///   <para>
///     Each publication creates a check run of the name of the status, with the first batch of annotations, then adds
///     the other batches to the same check run, by its identifier. GitHub shows the latest check run of a name on a
///     commit: a publication that changes the state or the description supersedes the earlier one.
///   </para>
///   <para>
///     The caller checks the forge first (ADR-014): check runs are a GitHub notion, never part of
///     <see cref="IForgeGateway" />.
///   </para>
/// </remarks>
internal sealed class CheckRunPublisher(IGitHubAppGateway gitHub)
{
  #region Methods

  /// <summary>Publishes a status as a check run.</summary>
  /// <param name="repository">The repository, on GitHub.</param>
  /// <param name="sha">The commit.</param>
  /// <param name="status">The status, whose context names the check run.</param>
  /// <param name="targetUrl">The address it links to, or <see langword="null" />.</param>
  /// <param name="annotations">The annotations of the check run, in order; none for most statuses.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   The identifier of the check run, or <see langword="null" /> when GitHub refused it (the Checks permission is not
  ///   granted): the caller publishes the status instead.
  /// </returns>
  /// <exception cref="ArgumentNullException">An argument other than <paramref name="targetUrl" /> is <see langword="null" />.</exception>
  public async Task<long?> PublishAsync(
    ForgeRepositoryRef repository,
    GitSha sha,
    CommitStatus status,
    string? targetUrl,
    IReadOnlyList<CheckRunAnnotation> annotations,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentNullException.ThrowIfNull(status);
    ArgumentNullException.ThrowIfNull(annotations);

    var batches = CheckRunAnnotationBuilder.Batch(annotations);
    var run = new GitHubCheckRun(status.Context, sha, status.State, status.Description, targetUrl, batches.Count > 0 ? batches[0] : []);

    if (await gitHub.CreateCheckRunAsync(repository, run, cancellationToken).ConfigureAwait(false) is not { } id)
    {
      return null;
    }

    foreach (var batch in batches.Skip(1))
    {
      // A batch refused or failed after the creation leaves a check run with fewer notes: its conclusion is right
      // already, and its identifier must reach the caller, or a retry of the job would create another check run.
      try
      {
        if (!await gitHub.UpdateCheckRunAsync(repository, id, run with { Annotations = batch }, cancellationToken).ConfigureAwait(false))
        {
          break;
        }
      }
      catch (Exception exception) when (exception is not OperationCanceledException)
      {
        break;
      }
    }

    return id;
  }

  /// <summary>Gives an earlier check run the state of the status that now replaces it, so that both say the same.</summary>
  /// <param name="repository">The repository, on GitHub.</param>
  /// <param name="checkRunId">The identifier of the earlier check run.</param>
  /// <param name="sha">The commit.</param>
  /// <param name="status">The status that replaces it.</param>
  /// <param name="targetUrl">The address it links to, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes once GitHub answered; a refusal is ignored, the status is the publication now.</returns>
  /// <exception cref="ArgumentNullException">An argument other than <paramref name="targetUrl" /> is <see langword="null" />.</exception>
  public async Task SupersedeAsync(
    ForgeRepositoryRef repository,
    long checkRunId,
    GitSha sha,
    CommitStatus status,
    string? targetUrl,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentNullException.ThrowIfNull(status);

    var run = new GitHubCheckRun(status.Context, sha, status.State, status.Description, targetUrl, []);

    await gitHub.UpdateCheckRunAsync(repository, checkRunId, run, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
