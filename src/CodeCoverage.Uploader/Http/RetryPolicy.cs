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
  /// <exception cref="ArgumentNullException"><paramref name="send" /> is <see langword="null" />.</exception>
  /// <exception cref="HttpRequestException">The last attempt failed.</exception>
  public async Task<HttpResponseMessage> SendAsync(Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(send);

    for (var attempt = 1; ; attempt++)
    {
      TimeSpan wait;
      string reason;

      try
      {
        var response = await send(cancellationToken).ConfigureAwait(false);

        if (!IsRetryable(response) || attempt == MaxAttempts)
        {
          return response;
        }

        using (response)
        {
          wait = RetryAfter(response) ?? Backoff(attempt);
          reason = await DescribeAsync(response, cancellationToken).ConfigureAwait(false);
        }
      }
      catch (Exception exception) when (attempt < MaxAttempts && exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
      {
        wait = Backoff(attempt);
        reason = exception.Message.TrimEnd('.');
      }

      reporter.Warning($"Attempt {attempt} of {MaxAttempts} failed ({reason}): trying again in {wait.TotalSeconds:0.#} s.");
      await delay(wait, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Tells why a retryable answer failed: its HTTP status, and the <c>errorCode</c> of its problem details if it has one.</summary>
  /// <param name="response">The answer.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The reason, such as <c>HTTP 502</c> or <c>HTTP 502, Forge.Unavailable</c>.</returns>
  /// <remarks>
  ///   A body that cannot be read only loses its error code: the status is always given. The reading wraps every
  ///   error of the stream in an <see cref="HttpRequestException" />; a cancelled run stops at the wait that follows.
  /// </remarks>
  private static async Task<string> DescribeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var status = $"HTTP {(int)response.StatusCode}";

    try
    {
      var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

      return JsonBody.ReadString(body, "errorCode") is { } code ? $"{status}, {code}" : status;
    }
    catch (HttpRequestException)
    {
      return status;
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
