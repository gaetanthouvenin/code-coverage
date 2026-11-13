// ------------------------------------------------------------------------------------------------
// <copyright file="IsolationApiFactory.cs" company="Gaëtan Thouvenin">
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

namespace CodeCoverage.Api.Tests.Isolation;

// The REST API host of the isolation suite: the same host as in production, on a real database it migrates itself.
// Nothing is faked here, so what the suite measures is what an outsider would receive.
public sealed class IsolationApiFactory(string connectionString) : WebApplicationFactory<Program>
{
  #region Fields

  private readonly string _dataPath = Path.Combine(Path.GetTempPath(), $"codecoverage-isolation-{Guid.NewGuid():N}");

  #endregion

  #region Methods

  /// <inheritdoc />
  public override async ValueTask DisposeAsync()
  {
    await base.DisposeAsync();
    await Log.CloseAndFlushAsync();

    try
    {
      if (Directory.Exists(_dataPath))
      {
        Directory.Delete(_dataPath, recursive: true);
      }
    }
    catch (IOException)
    {
      // A leftover temporary directory must never fail the run.
    }
    catch (UnauthorizedAccessException)
    {
      // Same.
    }
  }

  /// <inheritdoc />
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.UseEnvironment("Testing");
    builder.UseSetting(SerilogConfiguration.FilePathKey, Path.Combine(_dataPath, "logs", "codecoverage-api-.log"));
    builder.UseSetting("ConnectionStrings:CodeCoverage", connectionString);
    builder.UseSetting("CodeCoverage:Database:Provider", "PostgreSql");

    // The host migrates its own database: the schema under test is the one a deployment creates.
    builder.UseSetting("CodeCoverage:Database:MigrateOnStartup", "true");
    builder.UseSetting("Worker:Enabled", "false");
    builder.UseSetting("DataProtection:AllowUnprotectedKeys", "true");

    builder.ConfigureTestServices(static services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
  }

  #endregion
}
