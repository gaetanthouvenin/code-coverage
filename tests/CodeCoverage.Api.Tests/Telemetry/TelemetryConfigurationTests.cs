// ------------------------------------------------------------------------------------------------
// <copyright file="TelemetryConfigurationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using CodeCoverage.Api.Configuration;

namespace CodeCoverage.Api.Tests.Telemetry;

// OpenTelemetry is registered only when an OTLP endpoint is configured. The providers are not built: an
// exporter is never started by these tests.
public sealed class TelemetryConfigurationTests
{
  #region Methods

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("  ")]
  public void AddTelemetry_WithoutOtlpEndpoint_ShouldRegisterNothing(string? endpoint)
  {
    var services = new ServiceCollection();

    services.AddTelemetry(Configuration(endpoint));

    services.ShouldBeEmpty();
  }

  [Fact]
  public void AddTelemetry_WithOtlpEndpoint_ShouldRegisterTracesAndMetrics()
  {
    var services = new ServiceCollection();

    services.AddTelemetry(Configuration("http://otel-collector:4317"));

    services.ShouldContain(d => d.ServiceType == typeof(TracerProvider));
    services.ShouldContain(d => d.ServiceType == typeof(MeterProvider));
  }

  [Theory]
  [InlineData("/health/live", false)]
  [InlineData("/health/ready", false)]
  [InlineData("/HEALTH/live", false)]
  [InlineData("/api/v1/uploads", true)]
  [InlineData("/healthy", true)]
  public void IsTraced_ShouldSkipTheHealthProbes(string path, bool expected)
  {
    var context = new DefaultHttpContext { Request = { Path = path } };

    TelemetryConfiguration.IsTraced(context).ShouldBe(expected);
  }

  [Fact]
  public void AddTelemetry_WithNullArguments_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => TelemetryConfiguration.AddTelemetry(null!, Configuration(null)));
    Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddTelemetry(null!));
  }

  private static IConfiguration Configuration(string? endpoint) =>
    new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?> { [TelemetryConfiguration.OtlpEndpointKey] = endpoint })
      .Build();

  #endregion
}
