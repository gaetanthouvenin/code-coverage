// ------------------------------------------------------------------------------------------------
// <copyright file="UploadStatusPoller.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using CodeCoverage.Uploader.Output;
using CodeCoverage.Uploader.Serialization;

namespace CodeCoverage.Uploader.Http;

/// <summary>Waits for the processing of an upload (<c>--wait</c>) and prints its log.</summary>
/// <param name="client">The HTTP client.</param>
/// <param name="delay">Waits between two questions.</param>
/// <param name="time">The clock that measures the timeout.</param>
/// <param name="reporter">Prints the messages.</param>
/// <remarks>
///   <para>
///     Only the address the server returned with the upload is asked: the uploader never builds it, so
///     that the server stays free to change its routes and its public host.
///   </para>
///   <para>
///     Nothing is thrown for what the server or the network does: a timeout, a refusal or an unreadable
///     answer ends the wait with a message, and the exit code follows <see cref="ExitCodePolicy" />.
///   </para>
/// </remarks>
internal sealed class UploadStatusPoller(HttpClient client, Func<TimeSpan, CancellationToken, Task> delay, TimeProvider time, ConsoleReporter reporter)
{
  #region Fields

  /// <summary>How long the processing is waited for.</summary>
  public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

  private static readonly TimeSpan FirstInterval = TimeSpan.FromSeconds(1);

  private static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(5);

  #endregion

  #region Methods

  /// <summary>Asks the state of an upload until its processing ends or the timeout is over.</summary>
  /// <param name="statusUrl">The address the server returned with the upload.</param>
  /// <param name="credential">The credential the upload was sent with.</param>
  /// <param name="cancellationToken">A token to cancel the wait.</param>
  /// <returns>
  ///   <see langword="true" /> when the upload was processed; <see langword="false" /> when its processing
  ///   failed, when the server refused to tell, or when the timeout is over.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="statusUrl" /> or <paramref name="credential" /> is <see langword="null" />.</exception>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
  public async Task<bool> PollAsync(Uri statusUrl, string credential, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(statusUrl);
    ArgumentNullException.ThrowIfNull(credential);

    var deadline = time.GetUtcNow() + Timeout;
    var interval = FirstInterval;

    reporter.Info("Waiting for the processing of the upload.");

    while (true)
    {
      var remaining = deadline - time.GetUtcNow();

      if (remaining <= TimeSpan.Zero)
      {
        break;
      }

      var (status, refused) = await AskAsync(statusUrl, credential, remaining, cancellationToken).ConfigureAwait(false);

      if (refused)
      {
        return false;
      }

      if (status?.State is UploadStatus.Processed or UploadStatus.Failed)
      {
        ProcessingLogPrinter.Print(reporter, status);

        return status.State is UploadStatus.Processed;
      }

      reporter.Detail($"The upload is {status?.State ?? "not readable yet"}.");

      remaining = deadline - time.GetUtcNow();

      if (remaining <= TimeSpan.Zero)
      {
        break;
      }

      await delay(interval < remaining ? interval : remaining, cancellationToken).ConfigureAwait(false);

      interval = interval + interval < MaxInterval ? interval + interval : MaxInterval;
    }

    reporter.Warning($"The upload is still being processed after {Timeout.TotalSeconds:0} s: its state is at {statusUrl}");

    return false;
  }

  /// <summary>Asks the state once.</summary>
  /// <param name="statusUrl">The address the server returned with the upload.</param>
  /// <param name="credential">The credential the upload was sent with.</param>
  /// <param name="remaining">What is left of the timeout: the question does not outlive it.</param>
  /// <param name="cancellationToken">A token to cancel the wait.</param>
  /// <returns>
  ///   The state, or <see langword="null" /> when it could not be read this time, and whether the server
  ///   refused for good.
  /// </returns>
  private async Task<(UploadStatus? Status, bool Refused)> AskAsync(Uri statusUrl, string credential, TimeSpan remaining, CancellationToken cancellationToken)
  {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    timeout.CancelAfter(remaining);

    try
    {
      using var message = new HttpRequestMessage(HttpMethod.Get, statusUrl);

      message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);

      using var response = await client.SendAsync(message, timeout.Token).ConfigureAwait(false);

      if (response.StatusCode is HttpStatusCode.OK)
      {
        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);

        return (JsonSerializer.Deserialize(body, UploaderJsonContext.Default.UploadStatus), false);
      }

      // The server is busy or restarting: the next question may be answered.
      if (response.StatusCode is HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
      {
        reporter.Detail($"The server answered {(int)response.StatusCode}: asking again.");

        return (null, false);
      }

      reporter.Error($"The server refused to give the state of the upload: {(int)response.StatusCode}.");

      return (null, true);
    }
    catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
    {
      reporter.Detail($"The state could not be read: {exception.Message}");

      return (null, false);
    }
  }

  #endregion
}
