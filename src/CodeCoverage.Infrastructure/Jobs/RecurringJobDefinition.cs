// ------------------------------------------------------------------------------------------------
// <copyright file="RecurringJobDefinition.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>A job queued again and again at a fixed period, by the <see cref="RecurringJobScheduler" />.</summary>
/// <remarks>
///   <para>
///     The runs are aligned on slots of <see cref="Interval" /> counted from the Unix epoch, in UTC: a daily
///     job runs at midnight UTC, a job every 5 minutes at <c>:00</c>, <c>:05</c>, <c>:10</c>, and so on. Each
///     request built by <see cref="ToRequest" /> runs at the start of the next slot.
///   </para>
///   <para>
///     The <see cref="DedupKey" /> is mandatory: it is the only guarantee that a recurring job is queued once,
///     whatever the number of replicas that schedule it. The requests are coalesced, so a request made while
///     the job of the next slot is already queued is dropped.
///   </para>
/// </remarks>
internal sealed class RecurringJobDefinition
{
  #region Fields

  /// <summary>The shortest period of a recurring job: the period of the scheduler itself.</summary>
  public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(1);

  /// <summary>The payload of a recurring job that takes no argument.</summary>
  public const string EmptyPayload = "{}";

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="RecurringJobDefinition" /> class.</summary>
  /// <param name="type">The type of the job, one of <see cref="JobTypes" />.</param>
  /// <param name="interval">The period of the job, at least <see cref="MinInterval" />.</param>
  /// <param name="dedupKey">The key that keeps a single queued job across the replicas.</param>
  /// <param name="lockKey">The key that serializes the job, or <see langword="null" />.</param>
  /// <param name="payloadJson">The payload of the job, as JSON; <see cref="EmptyPayload" /> by default.</param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="type" />, <paramref name="dedupKey" /> or <paramref name="payloadJson" /> is
  ///   <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   A text is empty, white space or longer than its maximum length (see <see cref="JobRequest" />).
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="interval" /> is shorter than <see cref="MinInterval" />.
  /// </exception>
  public RecurringJobDefinition(
    string type,
    TimeSpan interval,
    string dedupKey,
    string? lockKey = null,
    string payloadJson = EmptyPayload)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(interval, MinInterval);

    // The same checks as every request: a definition that cannot be queued fails at startup, not at its first slot.
    _ = JobRequest.Create(type, payloadJson, lockKey, dedupKey, coalesce: true);

    Type = type;
    Interval = interval;
    DedupKey = dedupKey;
    LockKey = lockKey;
    PayloadJson = payloadJson;
  }

  #endregion

  #region Properties

  /// <summary>Gets the type of the job, one of <see cref="JobTypes" />.</summary>
  public string Type { get; }

  /// <summary>Gets the period of the job.</summary>
  public TimeSpan Interval { get; }

  /// <summary>Gets the key that keeps a single queued job across the replicas.</summary>
  public string DedupKey { get; }

  /// <summary>Gets the key that serializes the job, or <see langword="null" />.</summary>
  public string? LockKey { get; }

  /// <summary>Gets the payload of the job, as JSON.</summary>
  public string PayloadJson { get; }

  #endregion

  #region Methods

  /// <summary>Gets the start of the slot that follows a given time.</summary>
  /// <param name="now">The current time.</param>
  /// <returns>The first slot boundary strictly after <paramref name="now" />, in UTC.</returns>
  public DateTimeOffset NextRun(DateTimeOffset now)
  {
    var elapsed = now.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks;

    // Integer arithmetic keeps the boundary exact; the division truncates toward zero, so floor it before the epoch.
    var slot = elapsed / Interval.Ticks;

    if (elapsed < 0 && elapsed % Interval.Ticks != 0)
    {
      slot--;
    }

    return DateTimeOffset.UnixEpoch.AddTicks((slot + 1) * Interval.Ticks);
  }

  /// <summary>Builds the request that queues the run of the next slot.</summary>
  /// <param name="now">The current time.</param>
  /// <returns>A coalesced request, deduplicated by <see cref="DedupKey" />, that runs at <see cref="NextRun" />.</returns>
  public JobRequest ToRequest(DateTimeOffset now) =>
    JobRequest.Create(Type, PayloadJson, LockKey, DedupKey, NextRun(now), coalesce: true);

  /// <inheritdoc />
  public override string ToString() => $"{Type} every {Interval} (dedup: {DedupKey})";

  #endregion
}
