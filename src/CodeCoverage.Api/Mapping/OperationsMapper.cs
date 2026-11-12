// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Operations;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Operations;
using CodeCoverage.Application.Features.Operations.Commands.RetryJob;
using CodeCoverage.Application.Features.Operations.Queries.GetSystemStatus;
using CodeCoverage.Application.Features.Operations.Queries.ListJobs;
using CodeCoverage.Application.Features.Operations.Queries.ListWebhookDeliveries;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the operations of the instance between the Domain and the API: metadata only.</summary>
internal static class OperationsMapper
{
  #region Methods

  /// <summary>Lists one page of the jobs and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="state">The state of the jobs, case-insensitive, or <see langword="null" /> for every state.</param>
  /// <param name="page">The number of the page, or <see langword="null" /> for the first.</param>
  /// <param name="pageSize">The size of a page, or <see langword="null" /> for the default one.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The page.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  /// <exception cref="BadRequestException">The state names no state of a job.</exception>
  public static async Task<JobPageResponse> GetJobPageResponseAsync(this ISender sender, string? state, int? page, int? pageSize, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    JobState? wanted = null;

    if (!string.IsNullOrEmpty(state))
    {
      wanted = Enum.TryParse<JobState>(state, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                 ? parsed
                 : throw new BadRequestException(OperationsErrors.InvalidPage, "The state names no state of a job.");
    }

    var jobs = await sender.Send(new ListJobsQuery(wanted, page ?? 1, pageSize ?? ListJobsQuery.DefaultPageSize), cancellationToken).ConfigureAwait(false);

    return new JobPageResponse([.. jobs.Items.Select(ToResponse)], jobs.Page, jobs.PageSize, jobs.TotalCount);
  }

  /// <summary>Lists the latest webhook deliveries and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="count">How many deliveries, or <see langword="null" /> for the default.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The deliveries, the latest first.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<WebhookDeliveryResponse>> GetWebhookDeliveryResponsesAsync(this ISender sender, int? count, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var deliveries = await sender.Send(new ListWebhookDeliveriesQuery(count ?? ListWebhookDeliveriesQuery.DefaultCount), cancellationToken).ConfigureAwait(false);

    return
    [
      .. deliveries.Select(static d => new WebhookDeliveryResponse(
                             d.DeliveryId,
                             d.Event,
                             d.Action,
                             d.OrganizationId,
                             d.InstallationId,
                             d.RepositoryGitHubId,
                             d.ReceivedAt,
                             d.ProcessedAt,
                             d.HasError)),
    ];
  }

  /// <summary>Reads the status of the system and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The status.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<SystemStatusResponse> GetSystemStatusResponseAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var status = await sender.Send(new GetSystemStatusQuery(), cancellationToken).ConfigureAwait(false);

    return new SystemStatusResponse(
      status.Queued,
      status.Running,
      status.Failed,
      status.Dead,
      status.OldestQueuedAge?.TotalSeconds,
      status.WorkerLastSeenAt,
      status.GitHubRemaining
    );
  }

  /// <summary>Retries a failed or dead job.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="id">The identifier of the job.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the job is queued again.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task RetryJobAsync(this ISender sender, long id, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new RetryJobCommand(id), cancellationToken).ConfigureAwait(false);
  }

  private static JobResponse ToResponse(JobSummary job) =>
    new(
      job.Id,
      job.OrganizationId,
      job.Type,
      job.State.ToString(),
      job.Attempts,
      job.MaxAttempts,
      job.RunAfter,
      job.FirstQueuedAt,
      job.LockedBy,
      job.LockedUntil,
      job.HasError,
      job.CorrelationId,
      job.CreatedAt,
      job.CompletedAt
    );

  #endregion
}
