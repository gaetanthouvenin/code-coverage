// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubHttpClientConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Registers the <c>github</c> HTTP client and its pipeline (ARCHITECTURE §8.2).</summary>
internal static class GitHubHttpClientConfiguration
{
  #region Fields

  /// <summary>The name of the HTTP client of the GitHub REST API.</summary>
  public const string ClientName = "github";

  /// <summary>
  ///   The name of the client of the web host of GitHub, where the tokens of the users are renewed. It has no retry:
  ///   a refresh token is accepted once, and a request sent again after a lost answer would be refused. Its timeout
  ///   is <see cref="OAuthTimeout" />: the renewal of a user holds the lock of that user.
  /// </summary>
  public const string OAuthClientName = "github-oauth";

  /// <summary>How long a renewal of a user token may take.</summary>
  public static readonly TimeSpan OAuthTimeout = TimeSpan.FromSeconds(30);

  /// <summary>
  ///   The option that marks a POST GitHub may receive twice without harm, such as a status or an installation token:
  ///   only such a POST is retried.
  /// </summary>
  public static readonly HttpRequestOptionsKey<bool> Repeatable = new("CodeCoverage.GitHub.Repeatable");

  /// <summary>The web host of GitHub; GitHub Enterprise Server is out of scope.</summary>
  private static readonly Uri OAuthBaseAddress = new("https://github.com/", UriKind.Absolute);

  private const string ApiVersion = "2022-11-28";

  private const string ApiVersionHeader = "X-GitHub-Api-Version";

  private const string MediaType = "application/vnd.github+json";

  private const string UserAgent = "CodeCoverage";

  #endregion

  #region Methods

  /// <summary>
  ///   Registers the <c>github</c> client, from the outside in: the rate limit handler, then the resilience pipeline;
  ///   <see cref="GitHubAppGateway" /> is its typed client.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The base address is read each time a client is created: a change of <c>GitHub:ApiBaseUrl</c> applies without a
  ///   restart. The pipeline keeps the 3 retries on a 5xx or a timeout and the circuit breaker of the standard handler,
  ///   but a 403 or a 429 is a limit to wait for, which neither retries nor counts as a failure of GitHub. A POST that
  ///   is not marked <see cref="Repeatable" /> is never retried: GitHub may have done it and lost its answer, and a
  ///   comment would be written twice, or the code of a manifest, accepted once, refused.
  /// </remarks>
  public static IServiceCollection AddGitHubHttpClient(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddSingleton<GitHubQuotaTracker>();
    services.AddTransient<RateLimitHandler>();
    services.AddHttpClient(ClientName, static (provider, client) => Configure(provider, client))
            .AddHttpMessageHandler<RateLimitHandler>()
            .AddStandardResilienceHandler(
              static resilience =>
              {
                resilience.Retry.ShouldHandle = static arguments => ValueTask.FromResult(
                                                  IsTransient(arguments.Outcome) && CanBeSentAgain(arguments.Context.GetRequestMessage())
                                                );
                resilience.CircuitBreaker.ShouldHandle = static arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));
              }
            );

    services.AddHttpClient<GitHubAppGateway>(ClientName);
    services.AddHttpClient<GitHubUserGateway>(ClientName);
    services.AddHttpClient(
      OAuthClientName,
      static client =>
      {
        client.BaseAddress = OAuthBaseAddress;
        client.Timeout = OAuthTimeout;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
      }
    );

    return services;
  }

  /// <summary>Points a new client at the API and sets the headers that every request carries.</summary>
  /// <param name="provider">The services of the host.</param>
  /// <param name="client">The new client.</param>
  private static void Configure(IServiceProvider provider, HttpClient client)
  {
    client.BaseAddress = provider.GetRequiredService<IOptionsMonitor<GitHubOptions>>().CurrentValue.GetApiBaseAddress();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaType));
    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    client.DefaultRequestHeaders.Add(ApiVersionHeader, ApiVersion);
  }

  /// <summary>Tells whether a request may be sent again after a failure.</summary>
  /// <param name="request">The request, or <see langword="null" /> when the pipeline does not know it.</param>
  /// <returns>
  ///   The value of <see cref="Repeatable" /> when the request sets it (a PATCH that appends, such as the annotations of a
  ///   check run, sets it to <see langword="false" />); otherwise <see langword="false" /> for a POST, and for an unknown
  ///   request.
  /// </returns>
  private static bool CanBeSentAgain(HttpRequestMessage? request) =>
    request is not null
    && (request.Options.TryGetValue(Repeatable, out var repeatable) ? repeatable : request.Method != HttpMethod.Post);

  /// <summary>Tells whether an outcome is a transient failure of GitHub.</summary>
  /// <param name="outcome">The outcome of an attempt.</param>
  /// <returns><see langword="true" /> for what the standard handler treats as transient, a 403 and a 429 excepted.</returns>
  private static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
    outcome.Result?.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
    && HttpClientResiliencePredicates.IsTransient(outcome);

  #endregion
}
