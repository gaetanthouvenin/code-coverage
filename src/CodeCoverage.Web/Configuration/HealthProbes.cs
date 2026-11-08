// ------------------------------------------------------------------------------------------------
// <copyright file="HealthProbes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using CodeCoverage.Infrastructure;

namespace CodeCoverage.Web.Configuration;

/// <summary>The health probes of the host.</summary>
public static class HealthProbes
{
  #region Fields

  /// <summary>The path of the liveness probe.</summary>
  public const string LivePath = "/health/live";

  /// <summary>The path of the readiness probe.</summary>
  public const string ReadyPath = "/health/ready";

  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  #endregion

  #region Methods

  /// <summary>Registers the health check services the probes rely on.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  public static IServiceCollection AddHealthProbes(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddHealthChecks();

    return services;
  }

  /// <summary>
  ///   Maps <c>/health/live</c> (the process answers) and <c>/health/ready</c> (the database answers,
  ///   every migration is applied and, under SQL Server, <c>READ_COMMITTED_SNAPSHOT</c> is on), both
  ///   anonymous.
  /// </summary>
  /// <param name="app">The endpoint route builder.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="app" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The Web never migrates: it stays not ready until the Api has applied the migrations, which is
  ///   why the Api is deployed first.
  /// </remarks>
  public static IEndpointRouteBuilder MapHealthProbes(this IEndpointRouteBuilder app)
  {
    ArgumentNullException.ThrowIfNull(app);

    app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync })
       .AllowAnonymous();

    app.MapHealthChecks(
         ReadyPath,
         new HealthCheckOptions
         {
           Predicate = c => c.Tags.Contains(DependencyInjection.ReadyTag),
           ResponseWriter = WriteAsync,
         }
       )
       .AllowAnonymous();

    return app;
  }

  private static Task WriteAsync(HttpContext context, HealthReport report)
  {
    context.Response.ContentType = "application/json; charset=utf-8";

    var body = new
    {
      status = report.Status.ToString(),
      checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString(), StringComparer.Ordinal),
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions), context.RequestAborted);
  }

  #endregion
}
