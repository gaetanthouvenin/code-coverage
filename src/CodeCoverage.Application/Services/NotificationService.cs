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

namespace CodeCoverage.Application.Services;

/// <summary>Publishes the notifications of a commit on its forge: its statuses.</summary>
/// <param name="comparisons">What builds the comparison of the commit.</param>
/// <param name="views">The cache of the views of the reports.</param>
/// <param name="repositoryFlags">The flags the repositories received coverage for.</param>
/// <param name="posted">The repository of the published statuses.</param>
/// <param name="gateways">The gateways of the forges.</param>
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

  /// <summary>Publishes the statuses of a commit.</summary>
  /// <param name="repository">The repository of the commit.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="commit">The commit, complete or in error.</param>
  /// <param name="config">The effective configuration of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>How many statuses were published on the forge.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public async Task<int> PublishStatusesAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    CoverageConfig config,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(config);

    var settings = await organizationSettings.GetAsync(cancellationToken).ConfigureAwait(false);

    if (!settings.StatusesEnabled)
    {
      return 0;
    }

    var format = new StatusFormat(settings.StatusContextPrefix, config.Precision, config.Rounding);
    var knownFlags = await repositoryFlags.ListAsync(repository.Id, cancellationToken).ConfigureAwait(false);
    IReadOnlyList<CommitStatus> statuses;
    int? pullNumber;

    if (commit.State is CommitState.Error)
    {
      pullNumber = commit.PullNumber;
      statuses = config.Notify.NotifyError ? FailedStatuses(commit, config, knownFlags, format, TargetUrl(repository, forge, commit, pullNumber)) : [];
    }
    else
    {
      var context = await comparisons.BuildAsync(repository, forge, commit, config, cancellationToken).ConfigureAwait(false);

      pullNumber = context.Resolution.PullRequest?.Number;
      statuses = Evaluate(commit, config, context, knownFlags, format);
    }

    return await PublishAsync(repository, forge, commit, statuses, TargetUrl(repository, forge, commit, pullNumber), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Builds the address a status links to: the page of the pull request, else the page of the commit.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="forge">The forge.</param>
  /// <param name="commit">The commit.</param>
  /// <param name="pullNumber">The number of the pull request, or <see langword="null" />.</param>
  /// <returns>The address in the Web console, or <see langword="null" /> when the instance has no public address.</returns>
  internal string? TargetUrl(TrackedRepository repository, Forge forge, Commit commit, int? pullNumber)
  {
    if (urls.CurrentValue.WebPublicUrl is not { Length: > 0 } web)
    {
      return null;
    }

    var owner = repository.Namespace.Length == 0 ? repository.Owner : $"{repository.Owner}:{repository.Namespace.Replace('/', ':')}";
    var page = pullNumber is { } number ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"pull/{number}") : $"commit/{commit.Sha.Value}";

    return $"{web.TrimEnd('/')}/{ForgeSegments.ToSegment(forge)}/{owner}/{repository.Name}/{page}";
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

  /// <summary>Publishes the statuses that differ from those already published, and stores them.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="forge">The forge.</param>
  /// <param name="commit">The commit.</param>
  /// <param name="statuses">The statuses of the commit.</param>
  /// <param name="targetUrl">The address the statuses link to.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>How many statuses were published.</returns>
  private async Task<int> PublishAsync(
    TrackedRepository repository,
    Forge forge,
    Commit commit,
    IReadOnlyList<CommitStatus> statuses,
    string? targetUrl,
    CancellationToken cancellationToken)
  {
    if (statuses.Count == 0)
    {
      return 0;
    }

    var known = (await posted.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false))
      .ToDictionary(static p => p.Status.Context, static p => p.Status, StringComparer.Ordinal);
    var gateway = gateways.GetGateway(forge);
    var address = repository.ToForgeRef(forge);
    var published = 0;

    // Two rules can give the same context (a named rule and a flag of the same name): the first one wins.
    foreach (var status in statuses.DistinctBy(static s => s.Context, StringComparer.Ordinal))
    {
      if (known.TryGetValue(status.Context, out var last) && last.State == status.State && string.Equals(last.Description, status.Description, StringComparison.Ordinal))
      {
        continue;
      }

      if (status.Context.Length > PostedStatus.ContextMaxLength)
      {
        LogContextTooLong(logger, commit.Id, status.Context.Length);

        continue;
      }

      await gateway.CreateStatusAsync(address, commit.Sha, status, targetUrl, cancellationToken).ConfigureAwait(false);
      await posted.UpsertAsync(new PostedStatus(commit.Id, status, targetUrl, clock.UtcNow), cancellationToken).ConfigureAwait(false);
      published++;
    }

    return published;
  }

  [LoggerMessage(EventId = 1400, Level = LogLevel.Warning, Message = "A status of commit {CommitId} is not published: its context holds {Length} characters")]
  private static partial void LogContextTooLong(ILogger logger, long commitId, int length);

  #endregion
}