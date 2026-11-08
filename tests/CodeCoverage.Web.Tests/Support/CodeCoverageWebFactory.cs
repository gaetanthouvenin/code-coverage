// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageWebFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Serilog;

using CodeCoverage.Web.Configuration;

namespace CodeCoverage.Web.Tests.Support;

// The Blazor Server host started as in production. One instance for every host test
// (CodeCoverageWebCollection): Serilog is static.
public sealed class CodeCoverageWebFactory : WebApplicationFactory<Program>
{
  #region Fields

  private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=codecoverage;Timeout=2";

  private readonly string _dataPath = Path.Combine(Path.GetTempPath(), $"codecoverage-web-tests-{Guid.NewGuid():N}");

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
    builder.UseSetting(SerilogConfiguration.FilePathKey, Path.Combine(_dataPath, "logs", "codecoverage-web-.log"));

    // A database that refuses every connection: the readiness probe reports it, nothing
    // else touches the database yet.
    builder.UseSetting("ConnectionStrings:CodeCoverage", UnreachableDatabase);

    // The tests that switch to Production carry no certificate: they accept unprotected keys explicitly.
    builder.UseSetting("DataProtection:AllowUnprotectedKeys", "true");

    // The key ring of the unreachable database (antiforgery) is replaced by an in-memory one; the
    // shared key ring is covered by the Infrastructure tests.
    builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
  }

  #endregion
}
