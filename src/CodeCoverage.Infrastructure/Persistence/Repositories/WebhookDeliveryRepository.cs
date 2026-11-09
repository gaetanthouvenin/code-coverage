// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>WebhookDeliveries</c> table, which is operational.</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="jobQueue">The queue that takes a job in the transaction of the change.</param>
/// <param name="wakeUpSignal">The signal that wakes the worker of this process once a job is committed.</param>
/// <param name="currentOrganization">The organization of the scope, read by <see cref="FindForProcessingAsync" /> only.</param>
internal sealed class WebhookDeliveryRepository(
  CodeCoverageDbContext context,
  SqlJobQueue jobQueue,
  JobWakeUpSignal wakeUpSignal,
  ICurrentOrganization currentOrganization) : IWebhookDeliveryRepository
{
  #region Fields

  /// <summary>The message of the refusal of a job that gives the delivery no lane.</summary>
  private const string NoLaneMessage = "The job of a delivery names its organization or runs on the lane of the instance.";

  #endregion

  #region Methods

  /// <summary>Compresses a body.</summary>
  /// <param name="payload">The raw body.</param>
  /// <returns>The gzip form: the fastest level, since a request waits for it.</returns>
  private static byte[] Compress(byte[] payload)
  {
    using var output = new MemoryStream();

    using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
    {
      gzip.Write(payload);
    }

    return output.ToArray();
  }

  /// <summary>Decompresses a body.</summary>
  /// <param name="compressed">The gzip form.</param>
  /// <returns>The raw body.</returns>
  private static byte[] Decompress(byte[] compressed)
  {
    using var input = new MemoryStream(compressed);
    using var gzip = new GZipStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream();

    gzip.CopyTo(output);

    return output.ToArray();
  }

  #endregion

  #region Implementation of IWebhookDeliveryRepository

  /// <inheritdoc />
  public async Task<bool> TryStoreAsync(GitHubWebhookEvent delivery, JobRequest job, DateTimeOffset at, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(delivery);
    ArgumentNullException.ThrowIfNull(job);
    ArgumentException.ThrowIf(job.OrganizationId is null && !job.IsInstance, NoLaneMessage, nameof(job));

    var record = new WebhookDeliveryRecord
    {
      DeliveryId = delivery.DeliveryId,
      OrganizationId = job.OrganizationId,
      Event = delivery.Event,
      Action = delivery.Action,
      InstallationId = delivery.InstallationId,
      RepositoryGitHubId = delivery.RepositoryId,
      Payload = Compress(delivery.Payload),
      ReceivedAt = at.ToUniversalTime(),
    };

    long jobId;
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      context.WebhookDeliveries.Add(record);

      try
      {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobId = await jobQueue.EnqueueInTransactionAsync(context, job, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (Exception exception) when (UniqueIndexViolation.Is(exception, WebhookDeliveryConfiguration.PrimaryKeyName))
      {
        return false;
      }
      finally
      {
        context.Entry(record).State = EntityState.Detached;
      }
    }

    // Committed: a worker of this process may claim the job at once instead of at its next poll.
    wakeUpSignal.Notify(jobId);

    return true;
  }

  /// <inheritdoc />
  public async Task<GitHubWebhookEvent?> FindForProcessingAsync(string deliveryId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(deliveryId);

    long? organizationId = currentOrganization.IsKnown ? currentOrganization.Id : null;

    var record = await context.WebhookDeliveries
                              .AsNoTracking()
                              .Where(d => d.DeliveryId == deliveryId)
                              .Where(d => d.OrganizationId == null || d.OrganizationId == organizationId)
                              .SingleOrDefaultAsync(cancellationToken)
                              .ConfigureAwait(false);

    return record is null
      ? null
      : new GitHubWebhookEvent(record.DeliveryId, record.Event, record.Action, record.InstallationId, record.RepositoryGitHubId, Decompress(record.Payload));
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<WebhookDeliverySummary>> ListRecentAsync(int count, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

    return await context.WebhookDeliveries
                        .AsNoTracking()
                        .OrderByDescending(static d => d.ReceivedAt)
                        .ThenBy(static d => d.DeliveryId)
                        .Take(count)
                        .Select(static d => new WebhookDeliverySummary(
                                  d.DeliveryId,
                                  d.Event,
                                  d.Action,
                                  d.OrganizationId,
                                  d.InstallationId,
                                  d.RepositoryGitHubId,
                                  d.ReceivedAt,
                                  d.ProcessedAt,
                                  d.Error))
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  #endregion
}
