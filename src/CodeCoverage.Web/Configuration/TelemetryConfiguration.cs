// ------------------------------------------------------------------------------------------------
// <copyright file="TelemetryConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using CodeCoverage.Infrastructure.Diagnostics;

namespace CodeCoverage.Web.Configuration;

/// <summary>Configures OpenTelemetry: the traces and the metrics of the Web, exported in OTLP.</summary>
/// <remarks>
///   <para>
///     Traces: ASP.NET Core (without the health probes) and <c>HttpClient</c> (GitHub sign-in); the Web runs no job,
///     so it does not listen to <see cref="CodeCoverageActivitySources.JobsName" />. Metrics: ASP.NET Core,
///     <c>HttpClient</c> and the meter of the application (<see cref="CodeCoverageMeter.Name" />).
///   </para>
///   <para>
///     Nothing is registered unless <see cref="OtlpEndpointKey" /> is set (environment variable or configuration):
///     without an endpoint, nothing is collected, nothing is exported and nothing fails. The exporter then reads
///     the standard <c>OTEL_EXPORTER_OTLP_*</c> variables (protocol, headers), and the resource carries the
///     service name <see cref="ServiceName" /> and the version of the assembly.
///   </para>
/// </remarks>
public static class TelemetryConfiguration
{
  #region Fields

  /// <summary>The configuration key (and environment variable) of the OTLP endpoint.</summary>
  public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

  /// <summary>The service name of the Web in the traces and the metrics.</summary>
  public const string ServiceName = "codecoverage-web";

  /// <summary>The root path of the health probes, which are never traced.</summary>
  private const string HealthPath = "/health";

  #endregion

  #region Methods

  /// <summary>Registers OpenTelemetry with the OTLP exporter when an endpoint is configured.</summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configuration">The configuration of the host, read for <see cref="OtlpEndpointKey" />.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="services" /> or <paramref name="configuration" /> is <see langword="null" />.
  /// </exception>
  public static IServiceCollection AddTelemetry(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    if (!IsOtlpEnabled(configuration))
    {
      return services;
    }

    services.AddOpenTelemetry()
            .ConfigureResource(static r => r.AddService(ServiceName, serviceVersion: ServiceVersion()))
            .WithTracing(static t => t.AddAspNetCoreInstrumentation(static o => o.Filter = IsTraced)
                                      .AddHttpClientInstrumentation())
            .WithMetrics(static m => m.AddAspNetCoreInstrumentation()
                                      .AddHttpClientInstrumentation()
                                      .AddMeter(CodeCoverageMeter.Name))
            .UseOtlpExporter();

    return services;
  }

  /// <summary>Tells whether an OTLP endpoint is configured.</summary>
  /// <param name="configuration">The configuration of the host.</param>
  /// <returns><see langword="true" /> when <see cref="OtlpEndpointKey" /> is set; otherwise <see langword="false" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="configuration" /> is <see langword="null" />.</exception>
  public static bool IsOtlpEnabled(IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    return !string.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]);
  }

  /// <summary>Tells whether a request is traced: the health probes never are.</summary>
  /// <param name="context">The context of the request.</param>
  /// <returns><see langword="false" /> for the paths under <c>/health</c>; otherwise <see langword="true" />.</returns>
  internal static bool IsTraced(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    return !context.Request.Path.StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>Reads the version of the Web.</summary>
  /// <returns>The informational version of the assembly, or <see langword="null" /> when it has none.</returns>
  private static string? ServiceVersion() =>
    typeof(TelemetryConfiguration).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

  #endregion
}
