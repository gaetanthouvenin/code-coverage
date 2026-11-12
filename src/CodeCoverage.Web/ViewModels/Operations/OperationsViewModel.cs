// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Operations;

/// <summary>The operations of the instance, metadata only: queue, failed and dead jobs, webhooks, workers and quotas.</summary>
/// <param name="Queued">The jobs waiting.</param>
/// <param name="Running">The jobs running.</param>
/// <param name="Failed">The jobs that failed for good.</param>
/// <param name="Dead">The jobs that used up their attempts.</param>
/// <param name="OldestQueuedAge">How long the oldest job due has been waiting, or <see langword="null" />.</param>
/// <param name="LastBeatAt">The latest beat of any worker, or <see langword="null" />.</param>
/// <param name="Jobs">The failed and dead jobs, the most recent first.</param>
/// <param name="Webhooks">The latest delivery of each event, the most recent first.</param>
/// <param name="Workers">The workers of the last 24 hours, the most recent first.</param>
/// <param name="Quotas">The GitHub quotas, the most consumed first.</param>
public sealed record OperationsViewModel(
  int Queued,
  int Running,
  int Failed,
  int Dead,
  TimeSpan? OldestQueuedAge,
  DateTimeOffset? LastBeatAt,
  IReadOnlyList<JobRowViewModel> Jobs,
  IReadOnlyList<WebhookEventViewModel> Webhooks,
  IReadOnlyList<WorkerViewModel> Workers,
  IReadOnlyList<QuotaViewModel> Quotas);
