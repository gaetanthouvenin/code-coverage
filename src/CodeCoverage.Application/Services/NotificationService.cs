// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationService.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Services;

/// <summary>Publishes the notifications of a commit on its forge: its statuses.</summary>
/// <param name="comparisons">What builds the comparison of the commit.</param>
/// <param name="views">The cache of the views of the reports.</param>
/// <param name="repositoryFlags">The flags the repositories received coverage for.</param>
/// <param name="posted">The repository of the published statuses.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="comments">What publishes the comment of a pull request.</param>
/// <param name="checkRuns">What publishes a status as a GitHub check run.</param>
/// <param name="organizationSettings">The settings of the organization of the scope.</param>
/// <param name="urls">The public addresses of the instance.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     A status is published on the SHA of the commit, which is the head of its pull request and never a
///     synthetic merge commit. It is published only when its state or its description differs from the one
///     stored in <c>PostedStatuses</c>: notifying a commit twice publishes nothing twice.
///   </para>
///   <para>
///     The prefix of the contexts and the switch of the statuses are settings of the organization
///     (<c>Notifications:StatusContextPrefix</c>, <c>Notifications:StatusesEnabled</c>).
///   </para>
/// </remarks>
internal sealed partial class NotificationService(
  ComparisonBuilder comparisons,
  IReportViewCache views,
  IPostedStatusRepository posted,
  IRepositoryFlagRepository repositoryFlags,
  IForgeGatewayProvider gateways,
  CommentPublisher comments,
  CheckRunPublisher checkRuns,
  IOrganizationSettingsProvider organizationSettings,
  IOptionsMonitor<UrlOptions> urls,
  IClock clock,
  ILogger<NotificationService> logger)
{
  #region Fields

  // The branches of a merge queue of GitHub: their commits count as those of a pull request.
  private const string MergeQueuePrefix = "gh-readonly-queue/";

  private const string ProcessingFailedMessage = "Coverage upload processing failed";

  #endregion

  #region Methods

  /// <summary>Notifies a commit on its forge: its statuses, then the comment of the pull request it is the head of.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="commit">The commit, finalized.</param>
  /// <param name="config">The configuration of the commit.</param>
  /// <param name="failedUploads">The number of uploads of the commit that failed.</param>
  /// <param name="configInvalid">Whether the configuration file of the commit could not be read and was ignored.</param>
  /// <param name="statusSuffix">
  ///   What is added to the description of every status, or <see langword="null" />: the notification of a stuck
  ///   commit says how many uploads it received.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The number of statuses published.</returns>
  /// <remarks>
  ///   <para>
  ///     One comparison serves the statuses and the comment. It is built as soon as the organization publishes either
  ///     of them (<c>Notifications:StatusesEnabled</c>, <c>Notifications:CommentsEnabled</c>).
  ///   </para>
  ///   <para>
  ///     A commit whose every upload failed has no comparison: it only gets the failed statuses of
  ///     <c>codecov.notify.notify_error</c>, and the comment of its pull request stays as it is.
  ///   </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public async Task<int> NotifyAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    CoverageConfig config,
    int failedUploads,
    bool configInvalid,
    string? statusSuffix,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(config);

    var settings = await organizationSettings.GetAsync(cancellationToken).ConfigureAwait(false);

    if (!settings.StatusesEnabled && !settings.CommentsEnabled)
    {
      return 0;
    }

    if (commit.State is CommitState.Error)
    {
      return settings.StatusesEnabled ? await PublishFailedStatusesAsync(repository, forge, commit, config, settings, cancellationToken).ConfigureAwait(false) : 0;
    }

    var context = await comparisons.BuildAsync(repository, forge, commit, config, cancellationToken).ConfigureAwait(false);
    var published = 0;

    if (settings.StatusesEnabled)
    {
      var format = new StatusFormat(settings.StatusContextPrefix, config.Precision, config.Rounding);
      var knownFlags = await repositoryFlags.ListAsync(repository.Id, cancellationToken).ConfigureAwait(false);
      IReadOnlyList<CommitStatus> statuses = Evaluate(commit, config, context, knownFlags, format);

      if (statusSuffix is not null)
      {
        // Never a pending status: the evaluated one is published, and says it rests on a part of the uploads.
        statuses = [.. statuses.Select(status => status.WithSuffix(statusSuffix))];
      }

      var targetUrl = TargetUrl(repository, forge, commit, context.Resolution.PullRequest?.Number);
      var checks = UsesChecks(forge, config, settings);
      IReadOnlyList<CheckRunAnnotation> annotations = checks && config.GitHubChecks.Annotations && context.HeadView is { } head
                                                        ? CheckRunAnnotationBuilder.Build(head, context.Resolution.PatchDiff)
                                                        : [];
      var patch = StatusContext.Build(settings.StatusContextPrefix, StatusType.Patch, StatusRule.DefaultName);

      published = await PublishAsync(repository, forge, commit, statuses, targetUrl, checks, patch, annotations, cancellationToken).ConfigureAwait(false);
    }

    // After the statuses: the pull request is saved once, with what the comment and the comparison change on it.
    await comments.PublishAsync(repository, forge, commit, config, context, failedUploads, configInvalid, settings.CommentsEnabled, cancellationToken)
                  .ConfigureAwait(false);

    return published;
  }

  /// <summary>Publishes the statuses of a commit whose every upload failed.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="commit">The commit.</param>
  /// <param name="config">The configuration of the commit.</param>
  /// <param name="settings">The settings of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The number of statuses published.</returns>
  private async Task<int> PublishFailedStatusesAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    CoverageConfig config,
    OrganizationSettingsSnapshot settings,
    CancellationToken cancellationToken)
  {
    var targetUrl = TargetUrl(repository, forge, commit, commit.PullNumber);

    if (!config.Notify.NotifyError)
    {
      return 0;
    }

    var format = new StatusFormat(settings.StatusContextPrefix, config.Precision, config.Rounding);
    var knownFlags = await repositoryFlags.ListAsync(repository.Id, cancellationToken).ConfigureAwait(false);

    return await PublishAsync(
               repository,
               forge,
               commit,
               FailedStatuses(commit, config, knownFlags, format, targetUrl),
               targetUrl,
               UsesChecks(forge, config, settings),
               patchContext: null,
               [],
               cancellationToken
             )
             .ConfigureAwait(false);
  }

  /// <summary>Builds the address a status links to: the page of the pull request, else the page of the commit.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="forge">The forge.</param>
  /// <param name="commit">The commit.</param>
  /// <param name="pullNumber">The number of the pull request, or <see langword="null" />.</param>
  /// <returns>The address in the Web console, or <see langword="null" /> when the instance has no public address.</returns>
  internal string? TargetUrl(TrackedRepository repository, Forge forge, Commit commit, int? pullNumber)
  {
    var links = new WebLinks(urls.CurrentValue.WebPublicUrl, forge, repository);

    return pullNumber is { } number ? links.Pull(number) : links.Commit(commit.Sha);
  }

  /// <summary>Tells whether a commit belongs to a pull request, or to the branch of a merge queue.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="hasPullRequest">Whether a pull request was found for the commit.</param>
  /// <returns><see langword="true" /> when the rules for pull requests apply.</returns>
  private static bool CountsAsPull(Commit commit, bool hasPullRequest) =>
    hasPullRequest || commit.Branch?.StartsWith(MergeQueuePrefix, StringComparison.Ordinal) is true;

  /// <summary>Builds the statuses of a commit whose every upload failed (<c>codecov.notify.notify_error</c>).</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="config">The configuration.</param>
  /// <param name="knownFlags">The flags the repository received coverage for.</param>
  /// <param name="format">How the statuses are named.</param>
  /// <param name="targetUrl">The address the statuses link to.</param>
  /// <returns>An error on each context the commit would have had.</returns>
  private static List<CommitStatus> FailedStatuses(
    Commit commit,
    CoverageConfig config,
    IReadOnlyList<string> knownFlags,
    StatusFormat format,
    string? targetUrl)
  {
    var rules = config.BuildStatusRules(knownFlags).Rules;

    // No session will ever carry a flag: the wait of a flag holds no error back, as Codecov only waits for a flag
    // once the report has sessions.
    var facts = StatusRuleFacts.From(CountsAsPull(commit, commit.PullNumber is not null), commit.Branch, [], config, rules) with
    {
      AfterNBuildsByFlag = new Dictionary<string, int>(StringComparer.Ordinal),
    };
    var description = targetUrl is null ? ProcessingFailedMessage : $"{ProcessingFailedMessage}, see {targetUrl}";

    return
    [
      .. rules.Where(rule => !StatusRuleFilter.Skip(rule, facts))
               .Select(rule => new CommitStatus(StatusContext.Build(format.Prefix, rule.Type, rule.Name), StatusState.Error, description)),
    ];
  }

  /// <summary>Evaluates every status rule of the configuration on a comparison.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="config">The configuration.</param>
  /// <param name="context">The comparison and what it was built from.</param>
  /// <param name="knownFlags">The flags the repository received coverage for.</param>
  /// <param name="format">How the statuses are named and their numbers written.</param>
  /// <returns>The statuses to publish, in the order of the rules.</returns>
  private List<CommitStatus> Evaluate(
    Commit commit,
    CoverageConfig config,
    ComparisonContext context,
    IReadOnlyList<string> knownFlags,
    StatusFormat format)
  {
    var resolution = context.Resolution;
    var comparedTo = resolution.ComparedTo;
    var sessions = context.HeadSessions.Select(static s => s.Session).ToList();
    var headReports = CommitMerger.ToSessionReports(config, context.HeadSessions);
    var baseReports = context.BaseSessions is null ? null : CommitMerger.ToSessionReports(config, context.BaseSessions);
    // As the `repository.flags` of Codecov: the flags the repository received coverage for, not the ones that are only
    // declared. Those of the head are among them: its finalization recorded them before it queued this notification.
    var flags = knownFlags.Concat(sessions.SelectMany(static s => s.Flags)).Distinct(StringComparer.Ordinal);
    var rules = config.BuildStatusRules(flags).Rules;
    var facts = StatusRuleFacts.From(CountsAsPull(commit, resolution.PullRequest is not null), commit.Branch, sessions, config, rules);
    var statuses = new List<CommitStatus>();

    foreach (var rule in rules)
    {
      // Each rule sees the two reports through its own flags and paths.
      var paths = StatusRuleFilter.PathsOf(rule, config);
      var head = views.GetOrCreate(commit.Id, commit.ReportRevision, headReports, rule.Flags, paths);
      var @base = comparedTo is null || baseReports is null
                    ? null
                    : views.GetOrCreate(comparedTo.Id, comparedTo.ReportRevision, baseReports, rule.Flags, paths);

      var subject = new StatusSubject(
        commit.Sha,
        head,
        @base,
        comparedTo?.Sha,
        resolution.ForkPoint,
        ComparisonCalculator.Patch(head, resolution.PatchDiff),
        resolution.ComparisonDiff,
        @base is null ? [] : ChangeDetector.Detect(@base.Report, head.Report, resolution.ComparisonDiff)
      );

      if (StatusEvaluator.Evaluate(rule, subject, facts, format) is { } status)
      {
        statuses.Add(status);
      }
    }

    return statuses;
  }

  /// <summary>Tells whether the statuses of a commit are published as GitHub check runs (M5.05, ADR-007).</summary>
  /// <param name="forge">The forge of the repository: check runs are GitHub's only (ADR-014).</param>
  /// <param name="config">The configuration of the commit: <c>github_checks: false</c> keeps the statuses.</param>
  /// <param name="settings">The settings of the organization: <c>Notifications:ChecksEnabled</c>, off by default.</param>
  /// <returns><see langword="true" /> when check runs replace the statuses.</returns>
  private static bool UsesChecks(Forge forge, CoverageConfig config, OrganizationSettingsSnapshot settings) =>
    forge is Forge.GitHub && settings.ChecksEnabled && config.GitHubChecks.Enabled;

  /// <summary>Publishes the statuses that differ from those already published, and stores them.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="forge">The forge.</param>
  /// <param name="commit">The commit.</param>
  /// <param name="statuses">The statuses of the commit.</param>
  /// <param name="targetUrl">The address the statuses link to.</param>
  /// <param name="checks">Whether they are published as GitHub check runs.</param>
  /// <param name="patchContext">The context of the default patch status, the one check run that carries annotations.</param>
  /// <param name="annotations">The annotations of the patch check run.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>How many statuses (or check runs) were published.</returns>
  /// <remarks>
  ///   <para>
  ///     A check run that GitHub refuses (the installation lacks the Checks permission) is published as a status, and so
  ///     are the next ones of this notification: the refusal never fails the job (ADR-007).
  ///   </para>
  ///   <para>
  ///     When a name switches between a status and a check run, the earlier publication stays on GitHub: it is given the
  ///     new state too (a status of the same context, or an update of the earlier check run), so that a required check
  ///     never keeps an old state.
  ///   </para>
  /// </remarks>
  private async Task<int> PublishAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    IReadOnlyList<CommitStatus> statuses,
    string? targetUrl,
    bool checks,
    string? patchContext,
    IReadOnlyList<CheckRunAnnotation> annotations,
    CancellationToken cancellationToken)
  {
    if (statuses.Count == 0)
    {
      return 0;
    }

    var known = (await posted.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false))
      .ToDictionary(static p => p.Status.Context, StringComparer.Ordinal);
    var gateway = gateways.GetGateway(forge);
    var address = repository.ToForgeRef(forge);
    var published = 0;

    // Two rules can give the same context (a named rule and a flag of the same name): the first one wins.
    foreach (var status in statuses.DistinctBy(static s => s.Context, StringComparer.Ordinal))
    {
      // Unchanged, and published the same way: a status that becomes a check run is published again.
      if (known.TryGetValue(status.Context, out var last)
          && last.Status.State == status.State
          && string.Equals(last.Status.Description, status.Description, StringComparison.Ordinal)
          && last.CheckRunId is not null == checks)
      {
        continue;
      }

      if (status.Context.Length > PostedStatus.ContextMaxLength)
      {
        LogContextTooLong(logger, commit.Id, status.Context.Length);

        continue;
      }

      long? checkRunId = null;

      if (checks)
      {
        var notes = string.Equals(status.Context, patchContext, StringComparison.Ordinal) ? annotations : [];

        checkRunId = await checkRuns.PublishAsync(address, commit.Sha, status, targetUrl, notes, cancellationToken).ConfigureAwait(false);

        if (checkRunId is null)
        {
          LogChecksRefused(logger, commit.Id);
          checks = false;
        }
      }

      if (checkRunId is null)
      {
        await gateway.CreateStatusAsync(address, commit.Sha, status, targetUrl, cancellationToken).ConfigureAwait(false);

        // A check run published earlier under this name keeps its state on GitHub: it takes the new one.
        if (last is { CheckRunId: { } earlier })
        {
          await checkRuns.SupersedeAsync(address, earlier, commit.Sha, status, targetUrl, cancellationToken).ConfigureAwait(false);
        }
      }
      else if (last is { CheckRunId: null })
      {
        // A status published earlier under this name keeps its state on GitHub, beside the check run: it takes the new one.
        await gateway.CreateStatusAsync(address, commit.Sha, status, targetUrl, cancellationToken).ConfigureAwait(false);
      }

      await posted.UpsertAsync(new PostedStatus(commit.Id, status, targetUrl, clock.UtcNow) { CheckRunId = checkRunId }, cancellationToken).ConfigureAwait(false);
      published++;
    }

    return published;
  }

  [LoggerMessage(EventId = 1400, Level = LogLevel.Warning, Message = "A status of commit {CommitId} is not published: its context holds {Length} characters")]
  private static partial void LogContextTooLong(ILogger logger, long commitId, int length);

  [LoggerMessage(
    EventId = 1403,
    Level = LogLevel.Warning,
    Message = "GitHub refused the check runs of commit {CommitId}: the installation lacks the Checks permission; its statuses are published instead"
  )]
  private static partial void LogChecksRefused(ILogger logger, long commitId);

  #endregion
}