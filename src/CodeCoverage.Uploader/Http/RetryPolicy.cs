// ------------------------------------------------------------------------------------------------
// <copyright file="RetryPolicy.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Http;

/// <summary>Tries a request again after a network error, a 5xx or a 429, 5 times in all (§5.3).</summary>
/// <param name="delay">Waits between two attempts.</param>
/// <param name="reporter">Prints the messages.</param>
internal sealed class RetryPolicy(Func<TimeSpan, CancellationToken, Task> delay, ConsoleReporter reporter)
{
  #region Fields

  /// <summary>The number of attempts.</summary>
  public const int MaxAttempts = 5;

  #endregion

  #region Methods

  /// <summary>Sends a request until it gets an answer that is not worth retrying.</summary>
  /// <param name="send">Sends one attempt.</param>
  /// <param name="cancellationToken">A token to cancel the waits.</param>
  /// <returns>The last answer.</returns>
  /// <exception cref="HttpRequestException">The last attempt failed.</exception>
  public async Task<HttpResponseMessage> SendAsync(Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
  {
    for (var attempt = 1; ; attempt++)
    {
      TimeSpan wait;

      try
      {
        var response = await send(cancellationToken).ConfigureAwait(false);

        if (!IsRetryable(response) || attempt == MaxAttempts)
        {
          return response;
        }

        wait = RetryAfter(response) ?? Backoff(attempt);
        response.Dispose();
      }
      catch (Exception exception) when (attempt < MaxAttempts && exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
      {
        wait = Backoff(attempt);
        reporter.Detail(exception.Message);
      }

      reporter.Warning($"Attempt {attempt} of {MaxAttempts} failed: trying again in {wait.TotalSeconds:0.#} s.");
      await delay(wait, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Tells whether an answer is worth another attempt.</summary>
  /// <param name="response">The answer.</param>
  /// <returns><see langword="true" /> for a 429 or a 5xx.</returns>
  private static bool IsRetryable(HttpResponseMessage response) =>
    response.StatusCode is HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;

  /// <summary>Reads how long the server asks to wait.</summary>
  /// <param name="response">The answer.</param>
  /// <returns>The delay, or <see langword="null" /> when the server gives none.</returns>
  private static TimeSpan? RetryAfter(HttpResponseMessage response)
  {
    var header = response.Headers.RetryAfter;

    return header?.Delta ?? (header?.Date is { } date ? Positive(date - DateTimeOffset.UtcNow) : null);
  }

  /// <summary>Keeps a delay above zero.</summary>
  /// <param name="delay">The delay.</param>
  /// <returns>The delay, or zero.</returns>
  private static TimeSpan Positive(TimeSpan delay) => delay > TimeSpan.Zero ? delay : TimeSpan.Zero;

  /// <summary>Computes the exponential backoff.</summary>
  /// <param name="attempt">The attempt that failed, from 1.</param>
  /// <returns>The delay: 1, 2, 4 then 8 seconds.</returns>
  private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));

  #endregion
}
