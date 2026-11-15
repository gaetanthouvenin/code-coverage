// ------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Localization;

using Serilog;

using CodeCoverage.Application;
using CodeCoverage.Infrastructure;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Web.Components;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Configuration.Localization;
using CodeCoverage.Web.Configuration.Security;
using CodeCoverage.Web.Configuration.Setup;
using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Extensions;
using CodeCoverage.Web.Localization;

// Each host reloads and freezes its own bootstrap logger, even next to another host of the same process.
var bootstrapLogger = SerilogConfiguration.CreateBootstrapLogger();

Log.Logger = bootstrapLogger;

try
{
  var builder = WebApplication.CreateBuilder(args);

  // The settings saved from the screen: the weakest source, after the command line, the environment and
  // appsettings.json. Secrets stay in their protected form in the configuration.
  builder.Configuration.AddCodeCoverageSettingsStore();

  SerilogConfiguration.UseSerilog(builder, bootstrapLogger);

  // The scheme and the client address the TLS proxy received, from the known proxies only (ARCHITECTURE §4bis.2).
  builder.Services.AddForwardedHeaders(builder.Configuration);

  // Traces and metrics, exported in OTLP only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
  builder.Services.AddTelemetry(builder.Configuration);

  builder.Services.AddApplication();
  builder.Services.AddInfrastructure();

  // The SQL of each engine (job claim dialect): the configured one is picked at run time.
  builder.Services.AddPostgreSqlProvider();
  builder.Services.AddSqlServerProvider();

  // The key ring is shared with the other host through the database; Production refuses unprotected keys.
  builder.Services.AddCodeCoverageDataProtection(builder.Configuration, builder.Environment);
  // The session cookie and the sign-in with GitHub, protected by the key ring above.
  builder.Services.AddCodeCoverageAuthentication();
  // The cookie of the first-start wizard, protected by the same key ring.
  builder.Services.AddSingleton<SetupCookie>();
  // The address that installs the GitHub App, shown by the wizard, the organizations of the instance and the home page.
  builder.Services.AddSingleton<AppInstallationLink>();
  builder.Services.AddCoverageUi();
  // The in-app documentation: the published documents embedded in the Web, rendered once per language.
  builder.Services.AddSingleton<DocumentationLibrary>();
  builder.Services.AddUiMessaging();
  builder.Services.AddHealthProbes();
  builder.Services.AddRazorComponents()
                  .AddInteractiveServerComponents();

  // Content-Security-Policy: frame-ancestors (SecurityHeadersMiddleware) supersedes X-Frame-Options.
  builder.Services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);

  var app = builder.Build();

  // First: everything after it, the sign-in callback included, sees the scheme of the proxy.
  app.UseForwardedHeaders();

  app.UseMiddleware<SecurityHeadersMiddleware>();

  // An unhandled error re-executes /error, and a body-less error status re-executes /error/{code}: the not-found page
  // answers /error/404, the error page every other code. Both run in a fresh scope so scoped state (renderer,
  // services) from the failed request cannot leak into the error page.
  app.UseExceptionHandler("/error", true);
  app.UseStatusCodePagesWithReExecute("/error/{0}", createScopeForStatusCodePages: true);

  // A status page is read with GET whatever the method of the refused request, then routed again: the routing comes
  // after it, never before.
  app.UseMiddleware<StatusPageRequestMiddleware>();
  app.UseRouting();

  // Until the GitHub App exists, every page leads to the first-start wizard.
  app.UseMiddleware<SetupRedirectMiddleware>();

  // The session of the request, before the antiforgery check that relies on it.
  app.UseAuthentication();
  app.UseAuthorization();

  // The interface speaks the languages of ConsoleLanguages (ADR-015): the culture cookie, then the language of the
  // browser, then English. No ?culture= parameter: a shared address never changes the language of who opens it.
  app.UseRequestLocalization(o =>
  {
    string[] languages = [.. ConsoleLanguages.Codes];

    o.AddSupportedCultures(languages).AddSupportedUICultures(languages).SetDefaultCulture(ConsoleLanguages.DefaultCode);
    o.RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()];
  });

  app.UseAntiforgery();

  app.MapStaticAssets();
  app.MapHealthProbes();
  app.MapAuthenticationEndpoints();
  app.MapCultureEndpoint();

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
