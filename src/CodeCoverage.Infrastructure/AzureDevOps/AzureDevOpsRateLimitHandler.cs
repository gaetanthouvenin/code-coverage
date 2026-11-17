// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsRateLimitHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;

using Microsoft.Extensions.Logging;

using Polly;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>
///   The outermost handler of the <c>azure-devops</c> client: it turns what the resilience pipeline could not absorb
///   into the exceptions of the application.
/// </summary>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     Azure DevOps throttles an identity that consumes too many of its resources: it answers 429 with the code
///     <c>TF400733</c>, a <c>Retry-After</c> header and the <c>X-RateLimit-*</c> headers. Such a refusal becomes a
///     <see cref="ForgeRateLimitedException" /> of <see cref="Forge.AzureDevOps" />, which the job worker waits for.
///   </para>
///   <para>
///     It wraps the resilience pipeline, so it sees the outcome of the last attempt only. A 4xx other than 429 is an
///     answer for the caller, returned as is, its body still readable.
///   </para>
/// </remarks>
internal sealed partial class AzureDevOpsRateLimitHandler(IClock clock, ILogger<AzureDevOpsRateLimitHandler> logger) : DelegatingHandler
{
  #region Fields

  private const string ResetHeader = "X-RateLimit-Reset";

  private const string ResourceHeader = "X-RateLimit-Resource";

  /// <summary>The wait assumed when a limit is signalled without any delay.</summary>
  private static readonly TimeSpan DefaultWait = TimeSpan.FromMinutes(1);

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="BadGatewayException">Azure DevOps still fails after the retries, or the circuit is open.</exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps refused the request because of a rate limit.</exception>
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var response = await SendThroughPipelineAsync(request, cancellationToken).ConfigureAwait(false);

    try
    {
      if (response.StatusCode is HttpStatusCode.TooManyRequests)
      {
        var retryAfter = GetRetryAfter(response);
        LogRateLimited(logger, ReadText(response, ResourceHeader) ?? "unknown", retryAfter);
        throw new ForgeRateLimitedException(Forge.AzureDevOps, retryAfter);
      }

      BadGatewayException.ThrowIfForgeRefused(
        (int)response.StatusCode >= 500,
        string.Create(CultureInfo.InvariantCulture, $"Azure DevOps is unavailable: HTTP {(int)response.StatusCode}.")
      );
    }
    catch
    {
      response.Dispose();
      throw;
    }

    return response;
  }

  /// <summary>Reads a text response header.</summary>
  /// <param name="response">The response.</param>
  /// <param name="name">The name of the header.</param>
  /// <returns>The first value, or <see langword="null" /> when the header is missing.</returns>
  private static string? ReadText(HttpResponseMessage response, string name) =>
    response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

  [LoggerMessage(EventId = 2551, Level = LogLevel.Warning, Message = "Azure DevOps rate limited a request on {Resource} until {RetryAfter}")]
  private static partial void LogRateLimited(ILogger logger, string resource, DateTimeOffset retryAfter);

  /// <summary>Works out when Azure DevOps accepts requests again.</summary>
  /// <param name="response">The refusal.</param>
  /// <returns>
  ///   The moment given by <c>Retry-After</c>; else <c>X-RateLimit-Reset</c>, in seconds since the epoch; else one
  ///   minute from now.
  /// </returns>
  private DateTimeOffset GetRetryAfter(HttpResponseMessage response)
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

    return long.TryParse(ReadText(response, ResetHeader), NumberStyles.None, CultureInfo.InvariantCulture, out var reset)
             ? DateTimeOffset.FromUnixTimeSeconds(reset)
             : clock.UtcNow + DefaultWait;
  }

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
      throw BadGatewayException.ForgeUnavailable("Azure DevOps does not answer.", exception);
    }
  }

  #endregion
}
