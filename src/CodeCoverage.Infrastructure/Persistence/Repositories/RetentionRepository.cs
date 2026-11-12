// ------------------------------------------------------------------------------------------------
// <copyright file="RetentionRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Deletes what the retention no longer keeps, in batches, with LINQ statements portable to both engines.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   <para>
///     The tables of an organization are read and deleted under the query filter of the scope; the operational tables
///     (<c>Jobs</c>, <c>WebhookDeliveries</c>) have none. Each batch selects its keys first, then deletes by them: the
///     statements never depend on a <c>DELETE … LIMIT</c> of an engine.
///   </para>
///   <para>
///     A commit is compacted by a guarded update first, on its state and with no upload still received or processing,
///     then only the commits that update compacted lose their reports, in one transaction: a finalization that runs at the
///     same time either wins, and the commit is not compacted, or comes after and is refused with <c>Commit.Compacted</c>.
///   </para>
/// </remarks>
internal sealed class RetentionRepository(CodeCoverageDbContext context) : IRetentionRepository
{
  #region Methods

  /// <summary>Runs batches until one handles fewer rows than its size.</summary>
  /// <param name="batch">Handles one batch and returns how many rows it handled.</param>
  /// <param name="batchSize">The size of a batch.</param>
  /// <returns>The number of rows handled.</returns>
  private static async Task<int> RepeatAsync(Func<Task<int>> batch, int batchSize)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

    var total = 0;
    int handled;

    do
    {
      handled = await batch().ConfigureAwait(false);
      total += handled;
    }
    while (handled >= batchSize);

    return total;
  }

  /// <summary>Compacts one batch of commits.</summary>
  /// <param name="before">The date before which a commit is compacted.</param>
  /// <param name="batchSize">The number of commits of a batch.</param>
  /// <param name="kept">The SHAs kept: heads of branches and SHAs of open pull requests.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The number of commits of the batch, compacted or skipped.</returns>
  private async Task<int> CompactBatchAsync(DateTimeOffset before, int batchSize, List<GitSha> kept, CancellationToken cancellationToken)
  {
    var candidates = await context.Commits
                                  .Where(c => (c.State == CommitState.Complete || c.State == CommitState.Error)
                                              && c.CompletedAt < before
                                              && c.Branch != null
                                              && context.Repositories.Any(r => r.Id == c.RepositoryId && r.DefaultBranch != c.Branch)
                                              && !kept.Contains(c.Sha)
                                              && !context.Uploads.Any(u => u.CommitId == c.Id && (u.State == UploadState.Received || u.State == UploadState.Processing))
                                              && !context.CommitSessions.Any(s => s.CommitId != c.Id
                                                                                  && (context.Uploads.Any(u => u.CommitId == c.Id && u.Id == s.DataUploadId)
                                                                                      || context.CommitSessions.Any(o => o.CommitId == c.Id && o.Id == s.SourceSessionId))))
                                  .OrderBy(static c => c.Id)
                                  .Select(static c => c.Id)
                                  .Take(batchSize)
                                  .ToListAsync(cancellationToken)
                                  .ConfigureAwait(false);

    if (candidates.Count == 0)
    {
      return 0;
    }

    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      await context.Commits
                   .Where(c => candidates.Contains(c.Id)
                               && (c.State == CommitState.Complete || c.State == CommitState.Error)
                               && !context.Uploads.Any(u => u.CommitId == c.Id && (u.State == UploadState.Received || u.State == UploadState.Processing)))
                   .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, CommitState.Compacted).SetProperty(c => c.Version, c => c.Version + 1), cancellationToken)
                   .ConfigureAwait(false);

      var compacted = context.Commits.Where(c => candidates.Contains(c.Id) && c.State == CommitState.Compacted).Select(static c => c.Id);

      await context.CommitReports.Where(r => compacted.Contains(r.CommitId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
      await context.CommitSessions
                   .Where(s => compacted.Contains(s.CommitId) && s.Data != null)
                   .ExecuteUpdateAsync(u => u.SetProperty(s => s.Data, (byte[]?)null), cancellationToken)
                   .ConfigureAwait(false);
      await context.UploadCoverages
                   .Where(r => context.Uploads.Any(u => u.Id == r.UploadId && compacted.Contains(u.CommitId)))
                   .ExecuteDeleteAsync(cancellationToken)
                   .ConfigureAwait(false);

      await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    return candidates.Count;
  }

  #endregion

  #region Implementation of IRetentionRepository

  /// <inheritdoc />
  public Task<int> PurgeRawUploadsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
    RepeatAsync(
      async () =>
      {
        var files = await context.UploadRawFiles
                                 .Where(f => f.ExpiresAt <= now)
                                 .OrderBy(static f => f.Id)
                                 .Select(static f => f.Id)
                                 .Take(batchSize)
                                 .ToListAsync(cancellationToken)
                                 .ConfigureAwait(false);

        if (files.Count == 0)
        {
          return 0;
        }

        await context.UploadRawChunks.Where(c => files.Contains(c.RawFileId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        return await context.UploadRawFiles.Where(f => files.Contains(f.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
      },
      batchSize
    );

  /// <inheritdoc />
  public async Task<int> CompactCommitsAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken)
  {
    var heads = await context.Branches.Select(static b => b.HeadSha).ToListAsync(cancellationToken).ConfigureAwait(false);
    var open = await context.PullRequests
                            .Where(static p => p.State == PullRequestState.Open)
                            .Select(static p => new { p.HeadSha, p.BaseShaGitHub, p.ForkPointSha, p.ComparedToSha })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
    var kept = heads.Concat(open.SelectMany(static p => new[] { p.HeadSha, p.BaseShaGitHub, p.ForkPointSha, p.ComparedToSha }))
                    .OfType<GitSha>()
                    .Distinct()
                    .ToList();

    return await RepeatAsync(() => CompactBatchAsync(before, batchSize, kept, cancellationToken), batchSize).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<int> PurgeForgeCachesAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken)
  {
    var diffs = await RepeatAsync(
                  () => context.DiffCache.Where(d => d.FetchedAt < before).OrderBy(static d => d.FetchedAt).Take(batchSize).ExecuteDeleteAsync(cancellationToken),
                  batchSize
                ).ConfigureAwait(false);
    var trees = await RepeatAsync(
                  () => context.RepositoryTrees.Where(t => t.FetchedAt < before).OrderBy(static t => t.FetchedAt).Take(batchSize).ExecuteDeleteAsync(cancellationToken),
                  batchSize
                ).ConfigureAwait(false);

    return diffs + trees;
  }

  /// <inheritdoc />
  public Task<int> PurgeJobsAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken) =>
    RepeatAsync(
      async () =>
      {
        var jobs = await context.Jobs
                                .Where(j => (j.State == JobState.Succeeded || j.State == JobState.Failed || j.State == JobState.Dead) && j.CompletedAt < before)
                                .OrderBy(static j => j.Id)
                                .Select(static j => j.Id)
                                .Take(batchSize)
                                .ToListAsync(cancellationToken)
                                .ConfigureAwait(false);

        return jobs.Count == 0 ? 0 : await context.Jobs.Where(j => jobs.Contains(j.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
      },
      batchSize
    );

  /// <inheritdoc />
  public Task<int> PurgeWebhookDeliveriesAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken) =>
    RepeatAsync(
      async () =>
      {
        var deliveries = await context.WebhookDeliveries
                                      .Where(d => d.ReceivedAt < before)
                                      .OrderBy(static d => d.ReceivedAt)
                                      .Select(static d => d.DeliveryId)
                                      .Take(batchSize)
                                      .ToListAsync(cancellationToken)
                                      .ConfigureAwait(false);

        return deliveries.Count == 0
                 ? 0
                 : await context.WebhookDeliveries.Where(d => deliveries.Contains(d.DeliveryId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
      },
      batchSize
    );

  #endregion
}
