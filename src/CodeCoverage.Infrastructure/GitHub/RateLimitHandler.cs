// ------------------------------------------------------------------------------------------------
// <copyright file="RateLimitHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;

using Microsoft.Extensions.Logging;

using Polly;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>
///   The outermost handler of the <c>github</c> client: it publishes the quota left and turns what the resilience
///   pipeline could not absorb into the exceptions of the application.
/// </summary>
/// <param name="meter">The metrics of the application.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   It wraps the resilience pipeline, so it sees the outcome of the last attempt only. A plain 403 or any other 4xx is
///   an answer for the caller, returned as is, its body still readable.
/// </remarks>
internal sealed partial class RateLimitHandler(
  CodeCoverageMeter meter,
  IClock clock,
  ILogger<RateLimitHandler> logger) : DelegatingHandler
{
  #region Fields

  /// <summary>The value of the <c>installation</c> tag for the requests authenticated as the App itself.</summary>
  public const string AppQuota = "app";

  /// <summary>
  ///   The request option that names the installation whose token authenticates the request; left unset for the
  ///   requests authenticated as the App itself.
  /// </summary>
  public static readonly HttpRequestOptionsKey<long> InstallationId = new("CodeCoverage.GitHub.InstallationId");

  /// <summary>
  ///   The option that marks a request sent with a user token: its quota is the one of that user, and is not
  ///   published under the installation nor under the App.
  /// </summary>
  public static readonly HttpRequestOptionsKey<bool> UserToken = new("CodeCoverage.GitHub.UserToken");

  private const string RemainingHeader = "x-ratelimit-remaining";

  private const string ResetHeader = "x-ratelimit-reset";

  /// <summary>The wait GitHub asks for when a limit is signalled without any delay.</summary>
  private static readonly TimeSpan DefaultWait = TimeSpan.FromMinutes(1);

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="BadGatewayException">GitHub still fails after the retries, or the circuit is open.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub refused the request because of a rate limit.</exception>
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var response = await SendThroughPipelineAsync(request, cancellationToken).ConfigureAwait(false);

    try
    {
      PublishQuota(request, response);

      if (await IsRateLimitedAsync(response, cancellationToken).ConfigureAwait(false))
      {
        var resetAt = GetResetAt(response);
        LogRateLimited(logger, (int)response.StatusCode, resetAt);
        throw new GitHubRateLimitedException(resetAt);
      }

      if ((int)response.StatusCode >= 500)
      {
        throw new BadGatewayException(
          GitHubErrors.Unavailable,
          string.Create(CultureInfo.InvariantCulture, $"GitHub is unavailable: HTTP {(int)response.StatusCode}.")
        );
      }
    }
    catch
    {
      response.Dispose();
      throw;
    }

    return response;
  }

  /// <summary>Reads a numeric response header.</summary>
  /// <param name="response">The response.</param>
  /// <param name="name">The name of the header.</param>
  /// <returns>The value, or <see langword="null" /> when the header is missing or is not a number.</returns>
  private static long? ReadNumber(HttpResponseMessage response, string name) =>
    response.Headers.TryGetValues(name, out var values)
    && long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
      ? value
      : null;

  /// <summary>Tells whether a response is a refusal because of a primary or a secondary rate limit.</summary>
  /// <param name="response">The response.</param>
  /// <param name="cancellationToken">A token to cancel the reading of the body.</param>
  /// <returns><see langword="true" /> when the response signals a rate limit.</returns>
  private static async Task<bool> IsRateLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    if (response.StatusCode is HttpStatusCode.TooManyRequests)
    {
      return true;
    }

    if (response.StatusCode is not HttpStatusCode.Forbidden)
    {
      return false;
    }

    if (response.Headers.RetryAfter is not null || ReadNumber(response, RemainingHeader) is 0)
    {
      return true;
    }

    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

    return body.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
  }

  [LoggerMessage(EventId = 2501, Level = LogLevel.Warning, Message = "GitHub rate limited a request (HTTP {Status}) until {ResetAt}")]
  private static partial void LogRateLimited(ILogger logger, int status, DateTimeOffset resetAt);

  /// <summary>Sends a request through the resilience pipeline.</summary>
  /// <param name="request">The request.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The response of the last attempt.</returns>
  /// <exception cref="BadGatewayException">The network failed after the retries, a timeout expired or the circuit is open.</exception>
  private async Task<HttpResponseMessage> SendThroughPipelineAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    try
    {
      return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException)
    {
      throw new BadGatewayException(GitHubErrors.Unavailable, "GitHub does not answer.", exception);
    }
  }

  /// <summary>Publishes the quota left, under the installation that sent the request.</summary>
  /// <param name="request">The request.</param>
  /// <param name="response">The response.</param>
  private void PublishQuota(HttpRequestMessage request, HttpResponseMessage response)
  {
    if (request.Options.TryGetValue(UserToken, out var user) && user)
    {
      return;
    }

    if (ReadNumber(response, RemainingHeader) is not { } remaining)
    {
      return;
    }

    var installation = request.Options.TryGetValue(InstallationId, out var id)
                         ? id.ToString(CultureInfo.InvariantCulture)
                         : AppQuota;

    meter.ReportGitHubRateLimit(installation, remaining);
  }

  /// <summary>Works out when GitHub accepts requests again, following the order documented by GitHub.</summary>
  /// <param name="response">The refusal.</param>
  /// <returns>
  ///   The moment given by <c>retry-after</c>; else <c>x-ratelimit-reset</c> when the quota is empty; else one minute
  ///   from now.
  /// </returns>
  private DateTimeOffset GetResetAt(HttpResponseMessage response)
  {
    if (response.Headers.RetryAfter is { } retryAfter)
    {
      if (retryAfter.Delta is { } delta)
      {
        return clock.UtcNow + delta;
      }

      if (retryAfter.Date is { } date)
      {
        return date;
      }
    }

    return ReadNumber(response, RemainingHeader) is 0 && ReadNumber(response, ResetHeader) is { } reset
             ? DateTimeOffset.FromUnixTimeSeconds(reset)
             : clock.UtcNow + DefaultWait;
  }

  #endregion
}
