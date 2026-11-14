// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageMeter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace CodeCoverage.Infrastructure.Diagnostics;

/// <summary>
///   The metrics of the application: the <see cref="Meter" /> <see cref="Name" /> and its instruments
///   (ARCHITECTURE §12.6).
/// </summary>
/// <remarks>
///   <para>
///     A singleton of both hosts. The meter is created by the <see cref="IMeterFactory" /> of the host, which owns
///     and disposes it. The counters and the histograms are recorded by the features that observe the event; the
///     gauges publish the last value reported to the meter (<see cref="ReportJobQueue" />,
///     <see cref="ReportGitHubRateLimit" />), so that a collection never queries the database.
///   </para>
///   <para>
///     Recording costs next to nothing while nobody listens: without an OTLP endpoint, nothing is collected and
///     nothing is exported.
///   </para>
/// </remarks>
public sealed class CodeCoverageMeter
{
  #region Fields

  /// <summary>The name of the meter, to which the hosts subscribe.</summary>
  public const string Name = "CodeCoverage";

  /// <summary>The counter of the received uploads, tagged <c>repo</c> and <c>flag</c>.</summary>
  public const string UploadsReceivedTotal = "uploads_received_total";

  /// <summary>The counter of the refused or failed uploads, tagged <c>reason</c>.</summary>
  public const string UploadsFailedTotal = "uploads_failed_total";

  /// <summary>The histogram of the parse durations of a report, in seconds, tagged <c>format</c>.</summary>
  public const string ParseDurationSeconds = "parse_duration_seconds";

  /// <summary>The counter of the report paths that match no file of the repository.</summary>
  public const string UnresolvedPathsTotal = "unresolved_paths_total";

  /// <summary>The gauge of the queued jobs, tagged <c>type</c>.</summary>
  public const string JobsQueued = "jobs_queued";

  /// <summary>The gauge of the wait of the oldest job that may run, in seconds.</summary>
  public const string JobOldestQueuedAgeSeconds = "job_oldest_queued_age_seconds";

  /// <summary>The counter of the jobs that used all their attempts.</summary>
  public const string JobsDeadTotal = "jobs_dead_total";

  /// <summary>The gauge of the dead jobs in the queue, which wait for an operator (M4.04).</summary>
  public const string JobsDead = "jobs_dead";

  /// <summary>The gauge of the age of the last beat of a worker, in seconds (M4.04).</summary>
  public const string WorkerHeartbeatAgeSeconds = "worker_heartbeat_age_seconds";

  /// <summary>The gauge of the size of the GitHub quota, tagged <c>installation</c> (M4.04).</summary>
  public const string GitHubRateLimitLimit = "github_ratelimit_limit";

  /// <summary>
  ///   The gauge of the GitHub requests left, as recorded in the database with the size of the quota, tagged
  ///   <c>installation</c> (M4.04): the numerator of the quota alert.
  /// </summary>
  public const string GitHubRateLimitRecordedRemaining = "github_ratelimit_recorded_remaining";

  /// <summary>The histogram of the delays from the last upload of a commit to its published status, in seconds.</summary>
  public const string NotifyLatencySeconds = "notify_latency_seconds";

  /// <summary>The gauge of the GitHub requests left in the quota, tagged <c>installation</c>.</summary>
  public const string GitHubRateLimitRemaining = "github_ratelimit_remaining";

  /// <summary>The counter of the GitHub requests, tagged <c>endpoint</c> and <c>status</c>.</summary>
  public const string GitHubRequestsTotal = "github_requests_total";

  /// <summary>The counter of the webhook deliveries whose signature is invalid.</summary>
  public const string WebhookSignatureFailuresTotal = "webhook_signature_failures_total";

  private readonly Counter<long> _githubRequests;

  private readonly ConcurrentDictionary<string, long> _githubRateLimit = new(StringComparer.Ordinal);

  private readonly Counter<long> _jobsDead;

  private readonly ObservableGauge<double> _jobOldestQueuedAge;

  private readonly ObservableGauge<long> _jobsQueued;

  private readonly ObservableGauge<long> _jobsDeadGauge;

  private readonly ObservableGauge<double> _workerHeartbeatAge;

  private readonly ObservableGauge<long> _githubRateLimitLimit;

  private readonly ObservableGauge<long> _githubRateLimitRecordedRemaining;

  private readonly ConcurrentDictionary<string, byte> _knownJobTypes = new(StringComparer.Ordinal);

  private readonly Histogram<double> _notifyLatency;

  private readonly Histogram<double> _parseDuration;

  private readonly Counter<long> _unresolvedPaths;

  private readonly Counter<long> _uploadsFailed;

  private readonly Counter<long> _uploadsReceived;

  private readonly Counter<long> _webhookSignatureFailures;

  private JobQueueSnapshot? _jobQueue;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CodeCoverageMeter" /> class.</summary>
  /// <param name="meterFactory">The meter factory of the host, which owns the meter.</param>
  /// <exception cref="ArgumentNullException"><paramref name="meterFactory" /> is <see langword="null" />.</exception>
  public CodeCoverageMeter(IMeterFactory meterFactory)
  {
    ArgumentNullException.ThrowIfNull(meterFactory);

    var meter = meterFactory.Create(Name);

    _uploadsReceived = meter.CreateCounter<long>(UploadsReceivedTotal, "{upload}", "The received uploads.");
    _uploadsFailed = meter.CreateCounter<long>(UploadsFailedTotal, "{upload}", "The refused or failed uploads.");
    _parseDuration = meter.CreateHistogram<double>(ParseDurationSeconds, "s", "The parse duration of a report.");
    _unresolvedPaths = meter.CreateCounter<long>(
      UnresolvedPathsTotal,
      "{path}",
      "The report paths that match no file of the repository."
    );
    _jobsQueued = meter.CreateObservableGauge(JobsQueued, ObserveJobsQueued, "{job}", "The queued jobs, by type.");
    _jobOldestQueuedAge = meter.CreateObservableGauge(
      JobOldestQueuedAgeSeconds,
      ObserveOldestQueuedAge,
      "s",
      "How long the oldest job that may run has waited for a worker."
    );
    _jobsDead = meter.CreateCounter<long>(JobsDeadTotal, "{job}", "The jobs that used all their attempts.");
    _jobsDeadGauge = meter.CreateObservableGauge(JobsDead, ObserveJobsDead, "{job}", "The dead jobs in the queue, which wait for an operator.");
    _workerHeartbeatAge = meter.CreateObservableGauge(
      WorkerHeartbeatAgeSeconds,
      ObserveWorkerHeartbeatAge,
      "s",
      "How long ago a worker last beat."
    );
    _githubRateLimitLimit = meter.CreateObservableGauge(
      GitHubRateLimitLimit,
      ObserveGitHubRateLimitLimit,
      "{request}",
      "The size of the GitHub quota, by installation, as last recorded."
    );
    _githubRateLimitRecordedRemaining = meter.CreateObservableGauge(
      GitHubRateLimitRecordedRemaining,
      ObserveGitHubRateLimitRecordedRemaining,
      "{request}",
      "The GitHub requests left, by installation, as last recorded with the size of the quota."
    );
    _notifyLatency = meter.CreateHistogram<double>(
      NotifyLatencySeconds,
      "s",
      "The delay from the last upload of a commit to its published status."
    );
    meter.CreateObservableGauge(
      GitHubRateLimitRemaining,
      ObserveGitHubRateLimit,
      "{request}",
      "The GitHub requests left in the quota, by installation."
    );
    _githubRequests = meter.CreateCounter<long>(GitHubRequestsTotal, "{request}", "The GitHub requests.");
    _webhookSignatureFailures = meter.CreateCounter<long>(
      WebhookSignatureFailuresTotal,
      "{delivery}",
      "The webhook deliveries whose signature is invalid."
    );
  }

  #endregion

  #region Properties

  /// <summary>Gets a value indicating whether a listener collects the gauges of the jobs.</summary>
  /// <value>
  ///   <see langword="true" /> when a listener (the OpenTelemetry exporter, <c>dotnet-counters</c>, a test) enabled one
  ///   of the gauges the sampler feeds: <see cref="JobsQueued" />, <see cref="JobOldestQueuedAgeSeconds" />,
  ///   <see cref="JobsDead" />, <see cref="WorkerHeartbeatAgeSeconds" />, <see cref="GitHubRateLimitLimit" /> or
  ///   <see cref="GitHubRateLimitRecordedRemaining" />;
  ///   otherwise <see langword="false" />.
  /// </value>
  /// <remarks>
  ///   Read by <see cref="Jobs.JobQueueMetricsSampler" />, which does not query the database for gauges that
  ///   nobody collects.
  /// </remarks>
  internal bool IsJobQueueObserved =>
    _jobsQueued.Enabled
    || _jobOldestQueuedAge.Enabled
    || _jobsDeadGauge.Enabled
    || _workerHeartbeatAge.Enabled
    || _githubRateLimitLimit.Enabled
    || _githubRateLimitRecordedRemaining.Enabled;

  #endregion

  #region Methods

  /// <summary>Counts a GitHub request.</summary>
  /// <param name="endpoint">The endpoint template, such as <c>check-runs</c>; never a URL with identifiers.</param>
  /// <param name="status">The HTTP status code of the response.</param>
  /// <exception cref="ArgumentException"><paramref name="endpoint" /> is <see langword="null" />, empty or white space.</exception>
  public void GitHubRequest(string endpoint, int status)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

    _githubRequests.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint), new KeyValuePair<string, object?>("status", status));
  }

  /// <summary>Counts a job that used all its attempts and is now dead.</summary>
  public void JobDead() => _jobsDead.Add(1);

  /// <summary>Records the delay from the last upload of a commit to its published status.</summary>
  /// <param name="latency">The delay.</param>
  public void NotifyLatency(TimeSpan latency) => _notifyLatency.Record(latency.TotalSeconds);

  /// <summary>Records the parse duration of a report.</summary>
  /// <param name="format">The format of the report, such as <c>cobertura</c>.</param>
  /// <param name="duration">The parse duration.</param>
  /// <exception cref="ArgumentException"><paramref name="format" /> is <see langword="null" />, empty or white space.</exception>
  public void ParseDuration(string format, TimeSpan duration)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(format);

    _parseDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("format", format));
  }

  /// <summary>Reports the GitHub requests left in the quota of an installation, published by its gauge.</summary>
  /// <param name="installation">The identifier of the GitHub App installation.</param>
  /// <param name="remaining">The requests left, as read from <c>x-ratelimit-remaining</c>.</param>
  /// <exception cref="ArgumentException"><paramref name="installation" /> is <see langword="null" />, empty or white space.</exception>
  public void ReportGitHubRateLimit(string installation, long remaining)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(installation);

    _githubRateLimit[installation] = remaining;
  }

  /// <summary>Gives the requests left on the GitHub quota, by installation, as last read in this process.</summary>
  /// <returns>A copy, sorted by installation.</returns>
  public IReadOnlyDictionary<string, long> GitHubRateLimits() =>
    new SortedDictionary<string, long>(_githubRateLimit.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal), StringComparer.Ordinal);

  /// <summary>Counts the report paths of an upload that match no file of the repository.</summary>
  /// <param name="count">The number of unresolved paths; nothing is counted when it is 0 or less.</param>
  public void UnresolvedPaths(long count)
  {
    if (count > 0)
    {
      _unresolvedPaths.Add(count);
    }
  }

  /// <summary>Counts a refused or failed upload.</summary>
  /// <param name="reason">The reason, such as an error code; never a free text.</param>
  /// <exception cref="ArgumentException"><paramref name="reason" /> is <see langword="null" />, empty or white space.</exception>
  public void UploadFailed(string reason)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(reason);

    _uploadsFailed.Add(1, new KeyValuePair<string, object?>("reason", reason));
  }

  /// <summary>Counts a received upload.</summary>
  /// <param name="repository">The repository, as <c>owner/name</c>.</param>
  /// <param name="flag">The flag of the upload, or <see langword="null" /> when it has none.</param>
  /// <exception cref="ArgumentException"><paramref name="repository" /> is <see langword="null" />, empty or white space.</exception>
  public void UploadReceived(string repository, string? flag)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(repository);

    _uploadsReceived.Add(1, new KeyValuePair<string, object?>("repo", repository), new KeyValuePair<string, object?>("flag", flag ?? string.Empty));
  }

  /// <summary>Counts a webhook delivery whose signature is invalid.</summary>
  public void WebhookSignatureFailure() => _webhookSignatureFailures.Add(1);

  /// <summary>Reports the state of the job queue, published by the gauges of the jobs.</summary>
  /// <param name="snapshot">The state of the queue.</param>
  /// <exception cref="ArgumentNullException"><paramref name="snapshot" /> is <see langword="null" />.</exception>
  internal void ReportJobQueue(JobQueueSnapshot snapshot)
  {
    ArgumentNullException.ThrowIfNull(snapshot);

    Volatile.Write(ref _jobQueue, snapshot);
  }

  /// <summary>Publishes the dead jobs, once the queue was reported.</summary>
  /// <returns>The number of dead jobs, or no measurement before the first report.</returns>
  private IEnumerable<Measurement<long>> ObserveJobsDead() =>
    Volatile.Read(ref _jobQueue) is { } queue ? [new Measurement<long>(queue.Dead)] : [];

  /// <summary>Publishes the size of the GitHub quota of each installation, as the sampler last read it.</summary>
  /// <returns>One measurement per installation whose quota size is known.</returns>
  private IEnumerable<Measurement<long>> ObserveGitHubRateLimitLimit() =>
    Volatile.Read(ref _jobQueue) is { GitHubLimits: { } limits }
      ? limits.Select(static p => new Measurement<long>(p.Value, new KeyValuePair<string, object?>("installation", p.Key))).ToList()
      : [];

  /// <summary>Publishes the requests left on the GitHub quota of each installation, as the sampler last read them.</summary>
  /// <returns>One measurement per installation whose quota size is known.</returns>
  private IEnumerable<Measurement<long>> ObserveGitHubRateLimitRecordedRemaining() =>
    Volatile.Read(ref _jobQueue) is { GitHubRemaining: { } remaining }
      ? remaining.Select(static p => new Measurement<long>(p.Value, new KeyValuePair<string, object?>("installation", p.Key))).ToList()
      : [];

  /// <summary>Publishes the age of the last beat of a worker, once one was read.</summary>
  /// <returns>The age in seconds, or no measurement when no worker ever beat.</returns>
  private IEnumerable<Measurement<double>> ObserveWorkerHeartbeatAge() =>
    Volatile.Read(ref _jobQueue) is { HeartbeatAgeSeconds: { } age } ? [new Measurement<double>(age)] : [];

  /// <summary>Publishes the GitHub quota left of each installation reported so far.</summary>
  /// <returns>One measurement per installation.</returns>
  private IEnumerable<Measurement<long>> ObserveGitHubRateLimit() =>
    _githubRateLimit.Select(static p => new Measurement<long>(p.Value, new KeyValuePair<string, object?>("installation", p.Key)));

  /// <summary>Publishes the number of queued jobs of each type, once the queue was reported.</summary>
  /// <returns>
  ///   One measurement per job type seen so far: a type with no queued job left is published as 0, so that its
  ///   series drops instead of keeping its last value.
  /// </returns>
  private IEnumerable<Measurement<long>> ObserveJobsQueued()
  {
    if (Volatile.Read(ref _jobQueue) is not { } queue)
    {
      return [];
    }

    foreach (var type in queue.QueuedByType.Keys)
    {
      _knownJobTypes.TryAdd(type, 0);
    }

    return _knownJobTypes.Keys
                         .Select(type => new Measurement<long>(
                                   queue.QueuedByType.GetValueOrDefault(type),
                                   new KeyValuePair<string, object?>("type", type)
                                 ))
                         .ToList();
  }

  /// <summary>Publishes the wait of the oldest job that may run, once the queue was reported.</summary>
  /// <returns>The wait in seconds, or no measurement before the first report.</returns>
  private IEnumerable<Measurement<double>> ObserveOldestQueuedAge() =>
    Volatile.Read(ref _jobQueue) is { } queue ? [new Measurement<double>(queue.OldestDueAgeSeconds)] : [];

  #endregion
}
