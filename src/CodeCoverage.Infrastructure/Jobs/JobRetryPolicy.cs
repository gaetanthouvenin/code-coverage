// ------------------------------------------------------------------------------------------------
// <copyright file="JobRetryPolicy.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>When a failed job runs again: an exponential backoff, capped and spread.</summary>
/// <remarks>
///   The delay after the attempt <c>n</c> is <c>min(2^n × 10 s, 30 min)</c>, then spread by ±20 % so that
///   the jobs that failed together (GitHub unavailable) do not all run again at the same time.
/// </remarks>
internal static class JobRetryPolicy
{
  #region Fields

  /// <summary>The delay before the spread, for the attempt 0.</summary>
  public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(10);

  /// <summary>The longest delay before the spread.</summary>
  public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(30);

  /// <summary>The spread of the delay, as a fraction of it, on each side.</summary>
  public const double Jitter = 0.2;

  /// <summary>The highest exponent computed: beyond it, the delay is capped anyway.</summary>
  private const int MaxExponent = 20;

  #endregion

  #region Methods

  /// <summary>Gives the delay before a failed job runs again.</summary>
  /// <param name="attempts">How many attempts the job made, the failed one included.</param>
  /// <returns>The delay, spread at random.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempts" /> is negative.</exception>
  public static TimeSpan NextDelay(int attempts) => Delay(attempts, Random.Shared.NextDouble());

  /// <summary>Gives the delay before a failed job runs again, for a given draw of the spread.</summary>
  /// <param name="attempts">How many attempts the job made, the failed one included.</param>
  /// <param name="sample">
  ///   A number from 0 (included) to 1 (excluded): 0 gives the shortest delay (−20 %), 0.5 the delay itself
  ///   and nearly 1 the longest one (+20 %).
  /// </param>
  /// <returns>The delay.</returns>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="attempts" /> is negative, or <paramref name="sample" /> is not in [0, 1).
  /// </exception>
  internal static TimeSpan Delay(int attempts, double sample)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(attempts);
    ArgumentOutOfRangeException.ThrowIfNegative(sample);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sample, 1d);

    var exponential = BaseDelay * Math.Pow(2, Math.Min(attempts, MaxExponent));
    var capped = exponential < MaxDelay ? exponential : MaxDelay;

    return capped * (1 - Jitter + (2 * Jitter * sample));
  }

  #endregion
}
