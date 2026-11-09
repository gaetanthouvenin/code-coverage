// ------------------------------------------------------------------------------------------------
// <copyright file="JobRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>A request to run a job in the background: what to run, when, and how to merge it.</summary>
/// <remarks>
///   <para>
///     A job with a <see cref="LockKey" /> never runs while another job of the same key runs: the jobs
///     of a key are serialized (for example <c>commit:{id}</c>). A job without a lock key runs in
///     parallel with any other.
///   </para>
///   <para>
///     A job with a <see cref="DedupKey" /> is merged with the queued job of the same key, when there
///     is one (its <em>twin</em>); a running twin never blocks a new job, so no change is lost:
///   </para>
///   <list type="bullet">
///     <item>
///       <description>
///         with <see cref="Coalesce" />, the request is dropped: the twin, still queued, will see the
///         change when it runs;
///       </description>
///     </item>
///     <item>
///       <description>
///         otherwise the request debounces the twin: the twin keeps its payload and runs at
///         <see cref="RunAfter" />, but never later than <see cref="MaxDelay" /> after it was first
///         queued.
///       </description>
///     </item>
///   </list>
///   <para>
///     Every job belongs to a lane, chosen by the factory that builds its request: <see cref="Create" />
///     takes the organization of the current scope when the job is queued, <see cref="ForOrganization" />
///     names one, and <see cref="ForInstance" /> stays out of any organization. The queue prefixes both
///     keys with the lane: a key stays the business key (<c>repo:{id}</c>) and never collides with the
///     same key of another organization.
///   </para>
/// </remarks>
public sealed class JobRequest
{
  #region Fields

  /// <summary>The maximum length of <see cref="Type" />.</summary>
  public const int TypeMaxLength = 64;

  /// <summary>The maximum length of <see cref="LockKey" /> and <see cref="DedupKey" />.</summary>
  /// <remarks>
  ///   Leaves room for the prefix of the queue, <c>o{id}:</c> at 21 characters at most, in the column of
  ///   200 characters.
  /// </remarks>
  public const int KeyMaxLength = 178;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="JobRequest" /> class.</summary>
  /// <param name="isInstance">Whether the job stays out of any organization.</param>
  /// <param name="organizationId">The organization the request names, or <see langword="null" />.</param>
  /// <param name="type">The type of the job.</param>
  /// <param name="payloadJson">The payload of the job, as JSON.</param>
  /// <param name="lockKey">The key that serializes the job, or <see langword="null" />.</param>
  /// <param name="dedupKey">The key that merges the job with its queued twin, or <see langword="null" />.</param>
  /// <param name="runAfter">When the job may run, in UTC, or <see langword="null" /> for at once.</param>
  /// <param name="coalesce">Whether the request is dropped when a twin is queued.</param>
  /// <param name="maxDelay">How long a debounced job may wait since first queued, or <see langword="null" />.</param>
  private JobRequest(
    bool isInstance,
    long? organizationId,
    string type,
    string payloadJson,
    string? lockKey,
    string? dedupKey,
    DateTimeOffset? runAfter,
    bool coalesce,
    TimeSpan? maxDelay)
  {
    IsInstance = isInstance;
    OrganizationId = organizationId;
    Type = type;
    PayloadJson = payloadJson;
    LockKey = lockKey;
    DedupKey = dedupKey;
    RunAfter = runAfter;
    Coalesce = coalesce;
    MaxDelay = maxDelay;
  }

  #endregion

  #region Properties

  /// <summary>
  ///   Gets a value indicating whether the job stays out of any organization, as
  ///   <see cref="ForInstance" /> asks.
  /// </summary>
  public bool IsInstance { get; }

  /// <summary>
  ///   Gets the organization named by <see cref="ForOrganization" />, or <see langword="null" /> when the
  ///   request names none.
  /// </summary>
  public long? OrganizationId { get; }

  /// <summary>Gets the type of the job, one of <see cref="JobTypes" />.</summary>
  public string Type { get; }

  /// <summary>Gets the payload of the job, as JSON: the arguments of the command it runs.</summary>
  public string PayloadJson { get; }

  /// <summary>
  ///   Gets the key that serializes the job with the other jobs of the same key, or
  ///   <see langword="null" /> when the job may run in parallel with any other.
  /// </summary>
  public string? LockKey { get; }

  /// <summary>
  ///   Gets the key that merges the job with its queued twin, or <see langword="null" /> when every
  ///   request queues a new job.
  /// </summary>
  public string? DedupKey { get; }

  /// <summary>Gets when the job may run, in UTC, or <see langword="null" /> for at once.</summary>
  public DateTimeOffset? RunAfter { get; }

  /// <summary>
  ///   Gets a value indicating whether the request is dropped when a twin is queued; otherwise it
  ///   debounces the twin. Only with a <see cref="DedupKey" />.
  /// </summary>
  public bool Coalesce { get; }

  /// <summary>
  ///   Gets how long a debounced job may wait since it was first queued, or <see langword="null" />
  ///   when the debounce is not capped. Only with a <see cref="DedupKey" /> and without
  ///   <see cref="Coalesce" />.
  /// </summary>
  public TimeSpan? MaxDelay { get; }

  #endregion

  #region Methods

  /// <summary>Creates a request for the organization of the scope that queues it.</summary>
  /// <param name="type">The type of the job, one of <see cref="JobTypes" />.</param>
  /// <param name="payloadJson">The payload of the job, as JSON.</param>
  /// <param name="lockKey">The key that serializes the job, or <see langword="null" />.</param>
  /// <param name="dedupKey">The key that merges the job with its queued twin, or <see langword="null" />.</param>
  /// <param name="runAfter">When the job may run, converted to UTC, or <see langword="null" /> for at once.</param>
  /// <param name="coalesce">Whether the request is dropped when a twin is queued.</param>
  /// <param name="maxDelay">How long a debounced job may wait since first queued, or <see langword="null" />.</param>
  /// <returns>The request; queuing it outside an organization throws.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="type" /> or <paramref name="payloadJson" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="type" /> or <paramref name="payloadJson" /> is empty or white space, a text is
  ///   longer than its maximum length, a key is empty or white space, or <paramref name="coalesce" />
  ///   or <paramref name="maxDelay" /> is given without <paramref name="dedupKey" />.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="maxDelay" /> is zero or negative.
  /// </exception>
  public static JobRequest Create(
    string type,
    string payloadJson,
    string? lockKey = null,
    string? dedupKey = null,
    DateTimeOffset? runAfter = null,
    bool coalesce = false,
    TimeSpan? maxDelay = null) =>
    Build(isInstance: false, organizationId: null, type, payloadJson, lockKey, dedupKey, runAfter, coalesce, maxDelay);

  /// <summary>Creates a request that stays out of any organization: a coordinator or a maintenance job.</summary>
  /// <param name="type">The type of the job, one of <see cref="JobTypes" />.</param>
  /// <param name="payloadJson">The payload of the job, as JSON.</param>
  /// <param name="lockKey">The key that serializes the job, or <see langword="null" />.</param>
  /// <param name="dedupKey">The key that merges the job with its queued twin, or <see langword="null" />.</param>
  /// <param name="runAfter">When the job may run, converted to UTC, or <see langword="null" /> for at once.</param>
  /// <param name="coalesce">Whether the request is dropped when a twin is queued.</param>
  /// <param name="maxDelay">How long a debounced job may wait since first queued, or <see langword="null" />.</param>
  /// <returns>The request.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="type" /> or <paramref name="payloadJson" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="type" /> or <paramref name="payloadJson" /> is empty or white space, a text is
  ///   longer than its maximum length, a key is empty or white space, or <paramref name="coalesce" />
  ///   or <paramref name="maxDelay" /> is given without <paramref name="dedupKey" />.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="maxDelay" /> is zero or negative.
  /// </exception>
  public static JobRequest ForInstance(
    string type,
    string payloadJson,
    string? lockKey = null,
    string? dedupKey = null,
    DateTimeOffset? runAfter = null,
    bool coalesce = false,
    TimeSpan? maxDelay = null) =>
    Build(isInstance: true, organizationId: null, type, payloadJson, lockKey, dedupKey, runAfter, coalesce, maxDelay);

  /// <summary>Creates a request for an organization named by its identifier.</summary>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <param name="type">The type of the job, one of <see cref="JobTypes" />.</param>
  /// <param name="payloadJson">The payload of the job, as JSON.</param>
  /// <param name="lockKey">The key that serializes the job, or <see langword="null" />.</param>
  /// <param name="dedupKey">The key that merges the job with its queued twin, or <see langword="null" />.</param>
  /// <param name="runAfter">When the job may run, converted to UTC, or <see langword="null" /> for at once.</param>
  /// <param name="coalesce">Whether the request is dropped when a twin is queued.</param>
  /// <param name="maxDelay">How long a debounced job may wait since first queued, or <see langword="null" />.</param>
  /// <returns>The request; queuing it inside another organization throws.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="type" /> or <paramref name="payloadJson" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="type" /> or <paramref name="payloadJson" /> is empty or white space, a text is
  ///   longer than its maximum length, a key is empty or white space, or <paramref name="coalesce" />
  ///   or <paramref name="maxDelay" /> is given without <paramref name="dedupKey" />.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="organizationId" /> is not positive, or <paramref name="maxDelay" /> is zero or
  ///   negative.
  /// </exception>
  /// <remarks>
  ///   Reserved to the sources that act before a scope enters an organization: the reception of a webhook,
  ///   the coordinators, the admission of an organization and the refresh at sign-in (ADR-013).
  /// </remarks>
  public static JobRequest ForOrganization(
    long organizationId,
    string type,
    string payloadJson,
    string? lockKey = null,
    string? dedupKey = null,
    DateTimeOffset? runAfter = null,
    bool coalesce = false,
    TimeSpan? maxDelay = null)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organizationId);

    return Build(isInstance: false, organizationId, type, payloadJson, lockKey, dedupKey, runAfter, coalesce, maxDelay);
  }

  /// <summary>Tells which organization the job belongs to, once queued from a given scope.</summary>
  /// <param name="currentOrganizationId">
  ///   The organization the queuing scope has entered, or <see langword="null" /> when it has entered none.
  /// </param>
  /// <returns>The organization of the job, or <see langword="null" /> when it stays out of any.</returns>
  /// <exception cref="InvalidOperationException">
  ///   The request takes the organization of the scope and the scope has entered none, or the request names
  ///   an organization other than the one the scope has entered.
  /// </exception>
  public long? ResolveOrganizationId(long? currentOrganizationId)
  {
    if (IsInstance)
    {
      return null;
    }

    if (OrganizationId is not { } named)
    {
      return currentOrganizationId
             ?? throw new InvalidOperationException(
               $"The job '{this}' takes the organization of its scope, and the scope has not entered one."
             );
    }

    InvalidOperationException.ThrowIf(
      currentOrganizationId is not null && currentOrganizationId != named,
      $"The job '{this}' names an organization other than the one its scope has entered."
    );

    return named;
  }

  /// <inheritdoc />
  public override string ToString() => $"{Type} (lock: {LockKey ?? "-"}, dedup: {DedupKey ?? "-"})";

  /// <summary>Checks the arguments of a request and builds it.</summary>
  /// <param name="isInstance">Whether the job stays out of any organization.</param>
  /// <param name="organizationId">The organization the request names, or <see langword="null" />.</param>
  /// <param name="type">The type of the job, one of <see cref="JobTypes" />.</param>
  /// <param name="payloadJson">The payload of the job, as JSON.</param>
  /// <param name="lockKey">The key that serializes the job, or <see langword="null" />.</param>
  /// <param name="dedupKey">The key that merges the job with its queued twin, or <see langword="null" />.</param>
  /// <param name="runAfter">When the job may run, converted to UTC, or <see langword="null" /> for at once.</param>
  /// <param name="coalesce">Whether the request is dropped when a twin is queued.</param>
  /// <param name="maxDelay">How long a debounced job may wait since first queued, or <see langword="null" />.</param>
  /// <returns>The request.</returns>
  private static JobRequest Build(
    bool isInstance,
    long? organizationId,
    string type,
    string payloadJson,
    string? lockKey,
    string? dedupKey,
    DateTimeOffset? runAfter,
    bool coalesce,
    TimeSpan? maxDelay)
  {
    ThrowIfInvalid(type, TypeMaxLength);
    ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
    ThrowIfInvalidKey(lockKey);
    ThrowIfInvalidKey(dedupKey);

    ArgumentException.ThrowIf(
      dedupKey is null && (coalesce || maxDelay is not null),
      "Coalescing and debouncing need a deduplication key.",
      nameof(dedupKey)
    );

    if (maxDelay is { } delay)
    {
      ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delay, TimeSpan.Zero, nameof(maxDelay));

      ArgumentException.ThrowIf(
        coalesce,
        "A coalesced job is never debounced: it takes no maximum delay.",
        nameof(maxDelay)
      );
    }

    return new JobRequest(
      isInstance,
      organizationId,
      type,
      payloadJson,
      lockKey,
      dedupKey,
      runAfter?.ToUniversalTime(),
      coalesce,
      maxDelay
    );
  }

  /// <summary>Checks an optional key against its maximum length.</summary>
  /// <param name="key">The key, or <see langword="null" />.</param>
  /// <param name="name">The name of the parameter, filled by the compiler.</param>
  /// <exception cref="ArgumentException"><paramref name="key" /> is empty, white space or too long.</exception>
  private static void ThrowIfInvalidKey(string? key, [CallerArgumentExpression(nameof(key))] string? name = null)
  {
    if (key is not null)
    {
      ThrowIfInvalid(key, KeyMaxLength, name);
    }
  }

  /// <summary>Checks a mandatory text against its maximum length.</summary>
  /// <param name="value">The text.</param>
  /// <param name="maxLength">The maximum length.</param>
  /// <param name="name">The name of the parameter, filled by the compiler.</param>
  /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="value" /> is empty, white space or too long.</exception>
  private static void ThrowIfInvalid(
    string value,
    int maxLength,
    [CallerArgumentExpression(nameof(value))] string? name = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value, name);

    ArgumentException.ThrowIf(
      value.Length > maxLength,
      $"The value cannot be longer than {maxLength} characters.",
      name
    );
  }

  #endregion
}
