// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Operations.Commands.RetryJob;
using CodeCoverage.Application.Features.Operations.Queries.GetSystemStatus;
using CodeCoverage.Application.Features.Operations.Queries.ListAuditEntries;
using CodeCoverage.Application.Features.Operations.Queries.ListJobs;
using CodeCoverage.Application.Features.Operations.Queries.ListWebhookDeliveries;
using CodeCoverage.Application.Features.Organizations.Queries.ListOrganizations;
using CodeCoverage.Domain.Audit;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Web.ViewModels.Operations;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the operations of the instance for the pages and converts them to view models: no page names a Domain type.</summary>
internal static class OperationsMapper
{
  #region Fields

  /// <summary>The number of entries of a page of the audit log.</summary>
  public const int AuditPageSize = ListAuditEntriesQuery.DefaultPageSize;

  /// <summary>The value of the filter that keeps every family.</summary>
  public const string AllFamilies = "all";

  /// <summary>How long a worker may stay silent before it shows as not beating: the threshold of the suggested alert.</summary>
  public static readonly TimeSpan HeartbeatThreshold = TimeSpan.FromMinutes(2);

  // The actions with a label of their own; any other one is shown with its raw code.
  private static readonly HashSet<string> LabelledActions = new(StringComparer.Ordinal)
  {
    AuditCategories.ReplaceSecretAction,
    AuditCategories.ResetSettingAction,
    "Settings.Update",
    "Settings.CompleteSetup",
    "Settings.CompleteGitHubAppManifest",
    "Organization.Admit",
    "Organization.Suspend",
    "ApiTokens.Create",
    "ApiTokens.Revoke",
    AuditCategories.RetryJobAction,
  };

  #endregion

  #region Properties

  /// <summary>Gets the families the filter offers, as their names.</summary>
  public static IReadOnlyList<string> Families { get; } = [.. Enum.GetNames<AuditCategory>()];

  #endregion

  #region Methods

  /// <summary>Reads one page of the audit log of the instance.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="family">The name of the family kept, or <see cref="AllFamilies" />; an unknown one keeps every family.</param>
  /// <param name="search">A part of the actor, the action or the target, or <see langword="null" />; cut to its longest allowed length.</param>
  /// <param name="page">The page asked; kept within the pages a list can hold.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<AuditPageViewModel> GetAuditPageViewModelAsync(
    this ISender sender,
    string? family,
    string? search,
    int page,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    AuditCategory? category = Enum.TryParse<AuditCategory>(family, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    if (term?.Length > ListAuditEntriesQuery.SearchMaxLength)
    {
      term = term[..ListAuditEntriesQuery.SearchMaxLength];
    }

    var query = new ListAuditEntriesQuery(category, term, Math.Clamp(page, 1, ListAuditEntriesQuery.MaxPage), AuditPageSize);
    var entries = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return new AuditPageViewModel([.. entries.Items.Select(ToViewModel)], entries.Page, entries.PageSize, entries.TotalCount);
  }

  /// <summary>
  ///   Reads the operations of the instance: status, failed and dead jobs (with the login of their organization), the
  ///   latest delivery of each webhook event, workers and quotas; metadata only.
  /// </summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="now">The current time, to tell a silent worker.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The operations.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<OperationsViewModel> GetOperationsViewModelAsync(this ISender sender, DateTimeOffset now, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var status = await sender.Send(new GetSystemStatusQuery(), cancellationToken).ConfigureAwait(false);
    var dead = await sender.Send(new ListJobsQuery(JobState.Dead), cancellationToken).ConfigureAwait(false);
    var failed = await sender.Send(new ListJobsQuery(JobState.Failed), cancellationToken).ConfigureAwait(false);
    var deliveries = await sender.Send(new ListWebhookDeliveriesQuery(ListWebhookDeliveriesQuery.MaxCount), cancellationToken).ConfigureAwait(false);
    var organizations = (await sender.Send(new ListOrganizationsQuery(), cancellationToken).ConfigureAwait(false))
                        .ToDictionary(static o => o.Organization.Id, static o => o.Organization.Login);

    return new OperationsViewModel(
      status.Queued,
      status.Running,
      status.Failed,
      status.Dead,
      status.OldestQueuedAge,
      status.WorkerLastSeenAt,
      [
        .. dead.Items.Concat(failed.Items)
               .OrderByDescending(static j => j.Id)
               .Select(j => new JobRowViewModel(
                         j.Id,
                         j.Type,
                         j.OrganizationId is { } id ? organizations.GetValueOrDefault(id) : null,
                         j.State is JobState.Dead,
                         j.Attempts,
                         j.MaxAttempts,
                         j.CompletedAt ?? j.CreatedAt)),
      ],
      [
        .. deliveries.GroupBy(static d => d.Event, StringComparer.Ordinal)
                     .Select(static g => g.MaxBy(static d => d.ReceivedAt)!)
                     .OrderByDescending(static d => d.ReceivedAt)
                     .Select(static d => new WebhookEventViewModel(d.Event, d.ReceivedAt, d.ProcessedAt is not null, d.HasError)),
      ],
      [.. status.Workers.Select(w => new WorkerViewModel(w.Host, w.LastBeatAt, now - w.LastBeatAt <= HeartbeatThreshold))],
      [
        // The current quotas first, the most consumed first; a quota whose window started again since is refilled, last.
        .. status.GitHubQuotas.Select(q => new QuotaViewModel(q.Installation, q.Remaining, q.Limit, q.ResetAt, q.ResetAt <= now))
                 .OrderBy(static q => q.IsExpired)
                 .ThenBy(static q => q.RemainingPercent ?? double.MaxValue)
                 .ThenBy(static q => q.Installation, StringComparer.Ordinal),
      ]
    );
  }

  /// <summary>Retries a failed or dead job, under its own organization.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="id">The identifier of the job.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the job is queued again.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> RetryJobAsync(this ISender sender, long id, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new RetryJobCommand(id), cancellationToken).ConfigureAwait(false);

    return true;
  }

  private static AuditEntryViewModel ToViewModel(AuditEntrySummary entry)
  {
    // A reset of a secret is the clearing of that secret: it has its own label.
    var label = entry.Category is AuditCategory.Secrets && entry.Action == AuditCategories.ResetSettingAction
                  ? "AuditAction_Settings_ClearSecret"
                  : LabelledActions.Contains(entry.Action) ? "AuditAction_" + entry.Action.Replace('.', '_') : null;

    return new AuditEntryViewModel(entry.Id, entry.At, entry.Actor, entry.Action, label, entry.Category?.ToString(), entry.Target);
  }

  #endregion
}
