// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageApiFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Serilog;

using CodeCoverage.Api.Configuration;

namespace CodeCoverage.Api.Tests.Support;

// The REST API host started as in production, plus the test controllers of this assembly. One
// instance for every Api test (CodeCoverageApiCollection): Serilog is static.
public sealed class CodeCoverageApiFactory : WebApplicationFactory<Program>
{
  #region Fields

  private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=codecoverage;Timeout=2";

  private readonly string _dataPath = Path.Combine(Path.GetTempPath(), $"codecoverage-api-tests-{Guid.NewGuid():N}");

  #endregion

  #region Methods

  public override async ValueTask DisposeAsync()
  {
    await base.DisposeAsync();

    // WebApplicationFactory aborts Program before the reloadable logger is frozen, so the
    // CloseAndFlushAsync of Program never closes the frozen logger and its file handle.
    await Log.CloseAndFlushAsync();

    // Best-effort cleanup: a leftover temporary directory must never fail the run.
    try
    {
      if (Directory.Exists(_dataPath))
      {
        Directory.Delete(_dataPath, recursive: true);
      }
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Testing");

    // Never write the log files next to the sources.
    builder.UseSetting(SerilogConfiguration.FilePathKey, Path.Combine(_dataPath, "logs", "codecoverage-api-.log"));

    // A database that refuses every connection: the readiness probe reports it, nothing
    // else touches the database yet.
    builder.UseSetting("ConnectionStrings:CodeCoverage", UnreachableDatabase);

    // The host must start without a database: the migrator is covered by the Infrastructure tests.
    builder.UseSetting("CodeCoverage:Database:MigrateOnStartup", "false");

    // No job to run without a database: the worker is covered by the Infrastructure tests.
    builder.UseSetting("Worker:Enabled", "false");

    // The tests that switch to Production carry no certificate: they accept unprotected keys explicitly.
    builder.UseSetting("DataProtection:AllowUnprotectedKeys", "true");

    builder.ConfigureTestServices(services =>
    {
      services.AddControllers().AddApplicationPart(typeof(CodeCoverageApiFactory).Assembly);

      // The key ring of the unreachable database is replaced by an in-memory one; the shared key ring
      // is covered by the Infrastructure tests.
      services.AddDataProtection().UseEphemeralDataProtectionProvider();
    });
  }

  #endregion
}
