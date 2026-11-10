// ------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Serilog;

using CodeCoverage.Api.Configuration;
using CodeCoverage.Api.Configuration.Administration;
using CodeCoverage.Api.Configuration.Errors;
using CodeCoverage.Api.Configuration.Identity;
using CodeCoverage.Api.Configuration.RateLimiting;
using CodeCoverage.Api.Configuration.Uploads;
using CodeCoverage.Api.Configuration.Security;
using CodeCoverage.Application;
using CodeCoverage.Infrastructure;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.SqlServer;

// "admin ..." runs one administration command and exits: its arguments are not configuration.
var administration = AdminCommandLine.IsRequested(args);

Log.Logger = SerilogConfiguration.CreateBootstrapLogger(administration);

try
{
  var builder = WebApplication.CreateBuilder(administration ? [] : args);

  if (administration)
  {
    // The standard output is kept for the result of the command: the events go to the standard error.
    builder.Configuration.AddInMemoryCollection([new(SerilogConfiguration.ConsoleToStandardErrorKey, bool.TrueString)]);
  }

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

  // Only the Api migrates (unless CodeCoverage:Database:MigrateOnStartup is false), before it answers.
  builder.Services.AddDatabaseMigration();

  // Only the Api runs the background jobs (unless Worker:Enabled is false); the Web only queues them.
  builder.Services.AddJobProcessing();

  builder.Services.AddPresentation();
  builder.Services.AddApiDocumentation();
  builder.Services.AddForwardedHeaders(builder.Configuration);
  builder.Services.AddHealthProbes();
  builder.Services.AddValidateRateLimiter();
  builder.Services.AddUploadRateLimiter();
  builder.Services.AddProblemDetails();
  builder.Services.AddExceptionHandler<ApiExceptionHandler>();

  var app = builder.Build();

  if (administration)
  {
    // The host is never started: no Kestrel, no migration and no worker next to the running Api.
    await using (app.ConfigureAwait(false))
    {
      var commandLine = new AdminCommandLine(app.Services.GetRequiredService<IServiceScopeFactory>(), Console.In, Console.Out, Console.Error);

      return await commandLine.RunAsync(args, CancellationToken.None).ConfigureAwait(false);
    }
  }

  // The client address and scheme come from the known reverse proxies only.
  app.UseForwardedHeaders();
  app.UseMiddleware<SecurityHeadersMiddleware>();

  // The refusals of the Application become problem details; a bug becomes a 500 without detail.
  app.UseExceptionHandler();
  app.UseMiddleware<RequestBodyLimitMiddleware>();

  // The documentation is disabled in Production unless Features:EnableApiDocs is true.
  var enableDocs = app.IsApiDocumentationEnabled();

  if (enableDocs)
  {
    app.UseApiDocumentation();
  }

  app.UseRouting();
  app.UseRateLimiter();

  // Anonymous until the ApiToken scheme authenticates the request.
  app.UseMiddleware<HttpCurrentUserMiddleware>();

  app.MapControllers();
  app.MapHealthProbes();

  if (enableDocs)
  {
    app.MapScalarDocumentation();
  }

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

/// <summary>The entry point of the REST API host, exposed for <c>WebApplicationFactory</c>.</summary>
public partial class Program;
