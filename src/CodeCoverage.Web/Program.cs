// ------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Serilog;

using CodeCoverage.Application;
using CodeCoverage.Infrastructure;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Web.Components;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Configuration.Security;
using CodeCoverage.Web.Extensions;

Log.Logger = SerilogConfiguration.CreateBootstrapLogger();

try
{
  var builder = WebApplication.CreateBuilder(args);

  // The settings saved from the screen: the weakest source, after the command line, the environment and
  // appsettings.json. Secrets stay in their protected form in the configuration.
  builder.Configuration.AddCodeCoverageSettingsStore();

  builder.Host.UseSerilog(SerilogConfiguration.ConfigureLogger);

  // Traces and metrics, exported in OTLP only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
  builder.Services.AddTelemetry(builder.Configuration);

  builder.Services.AddApplication();
  builder.Services.AddInfrastructure();

  // The SQL of each engine (job claim dialect): the configured one is picked at run time.
  builder.Services.AddPostgreSqlProvider();
  builder.Services.AddSqlServerProvider();

  // The key ring is shared with the other host through the database; Production refuses unprotected keys.
  builder.Services.AddCodeCoverageDataProtection(builder.Configuration, builder.Environment);
  builder.Services.AddCoverageUi();
  builder.Services.AddUiMessaging();
  builder.Services.AddHealthProbes();
  builder.Services.AddRazorComponents()
                  .AddInteractiveServerComponents();

  // Content-Security-Policy: frame-ancestors (SecurityHeadersMiddleware) supersedes X-Frame-Options.
  builder.Services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);

  var app = builder.Build();

  app.UseMiddleware<SecurityHeadersMiddleware>();

  // An unhandled error re-executes /error and a body-less 404 re-executes /not-found, both in a fresh scope
  // so scoped state (renderer, services) from the failed request cannot leak into the error page.
  app.UseExceptionHandler("/error", true);
  app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

  // The interface speaks English and French, from the language of the browser.
  app.UseRequestLocalization(o => o.AddSupportedCultures("en", "fr")
                                   .AddSupportedUICultures("en", "fr")
                                   .SetDefaultCulture("en"));

  app.UseAntiforgery();

  app.MapStaticAssets();
  app.MapHealthProbes();

  // The frame-ancestors directive comes from SecurityHeadersMiddleware alone, for every response.
  app.MapRazorComponents<App>()
     .AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = null);

  Log.Information("{Application} starting", SerilogConfiguration.ApplicationName);
  await app.RunAsync().ConfigureAwait(false);
  return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
  Log.Fatal(ex, "{Application} failed to start", SerilogConfiguration.ApplicationName);
  throw;
}
finally
{
  await Log.CloseAndFlushAsync().ConfigureAwait(false);
}

/// <summary>The entry point of the Blazor Server host, exposed for <c>WebApplicationFactory</c>.</summary>
public partial class Program;
