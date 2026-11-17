// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsHttpClientConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>Registers the <c>azure-devops</c> HTTP client, its pipeline, and the client of Microsoft Entra ID.</summary>
internal static class AzureDevOpsHttpClientConfiguration
{
  #region Fields

  /// <summary>The name of the HTTP client of the Azure DevOps REST API.</summary>
  public const string ClientName = "azure-devops";

  /// <summary>
  ///   The name of the client of Microsoft Entra ID, where the application asks for its tokens. A client credentials
  ///   request may be sent twice without harm: it is retried like any transient failure. Its timeout is
  ///   <see cref="EntraTimeout" />.
  /// </summary>
  public const string EntraClientName = "azure-devops-entra";

  /// <summary>How long a token request to Microsoft Entra ID may take.</summary>
  public static readonly TimeSpan EntraTimeout = TimeSpan.FromSeconds(30);

  /// <summary>
  ///   The option that marks a POST Azure DevOps may receive twice without harm, such as a status: only such a POST is
  ///   retried.
  /// </summary>
  public static readonly HttpRequestOptionsKey<bool> Repeatable = new("CodeCoverage.AzureDevOps.Repeatable");

  private const string UserAgent = "CodeCoverage";

  #endregion

  #region Methods

  /// <summary>
  ///   Registers the <c>azure-devops</c> client, from the outside in: the rate limit handler, then the resilience
  ///   pipeline; and the client of Microsoft Entra ID.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The base address is read each time a client is created: a change of <c>AzureDevOps:ApiBaseUrl</c> applies
  ///   without a restart. The pipeline keeps the 3 retries on a 5xx or a timeout and the circuit breaker of the standard
  ///   handler, but a 429 is a limit to wait for, which neither retries nor counts as a failure of Azure DevOps. A POST
  ///   that is not marked <see cref="Repeatable" /> is never retried: Azure DevOps may have done it and lost its answer.
  ///   The gateways of Azure DevOps are the typed clients of this client.
  /// </remarks>
  public static IServiceCollection AddAzureDevOpsHttpClient(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddTransient<AzureDevOpsRateLimitHandler>();
    services.AddHttpClient(ClientName, static (provider, client) => Configure(provider, client))
            .AddHttpMessageHandler<AzureDevOpsRateLimitHandler>()
            .AddStandardResilienceHandler(
              static resilience =>
              {
                resilience.Retry.ShouldHandle = static arguments => ValueTask.FromResult(
                                                  IsTransient(arguments.Outcome) && CanBeSentAgain(arguments.Context.GetRequestMessage())
                                                );
                resilience.CircuitBreaker.ShouldHandle = static arguments => ValueTask.FromResult(IsTransient(arguments.Outcome));
              }
            );

    services.AddHttpClient(
              EntraClientName,
              static client =>
              {
                client.Timeout = EntraTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
              }
            )
            .AddStandardResilienceHandler();

    return services;
  }

  /// <summary>Points a new client at the API and sets the headers that every request carries.</summary>
  /// <param name="provider">The services of the host.</param>
  /// <param name="client">The new client.</param>
  private static void Configure(IServiceProvider provider, HttpClient client)
  {
    client.BaseAddress = provider.GetRequiredService<IOptionsMonitor<AzureDevOpsOptions>>().CurrentValue.GetApiBaseAddress();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
  }

  /// <summary>Tells whether a request may be sent again after a failure.</summary>
  /// <param name="request">The request, or <see langword="null" /> when the pipeline does not know it.</param>
  /// <returns>
  ///   The value of <see cref="Repeatable" /> when the request sets it; otherwise <see langword="false" /> for a POST, and
  ///   for an unknown request.
  /// </returns>
  private static bool CanBeSentAgain(HttpRequestMessage? request) =>
    request is not null
    && (request.Options.TryGetValue(Repeatable, out var repeatable) ? repeatable : request.Method != HttpMethod.Post);

  /// <summary>Tells whether an outcome is a transient failure of Azure DevOps.</summary>
  /// <param name="outcome">The outcome of an attempt.</param>
  /// <returns><see langword="true" /> for what the standard handler treats as transient, a 429 excepted.</returns>
  private static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
    outcome.Result?.StatusCode is not HttpStatusCode.TooManyRequests && HttpClientResiliencePredicates.IsTransient(outcome);

  #endregion
}
