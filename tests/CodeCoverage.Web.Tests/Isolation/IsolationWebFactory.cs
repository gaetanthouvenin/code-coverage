// ------------------------------------------------------------------------------------------------
// <copyright file="IsolationWebFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Serilog;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Isolation;

// The console of the isolation suite: the same host as in production, on a real database, with the session cookie
// stood in for by a header. Nothing else is faked, so what the suite measures is what a visitor would receive.
public sealed class IsolationWebFactory(string connectionString) : WebApplicationFactory<Program>
{
  #region Fields

  private readonly string _dataPath = Path.Combine(Path.GetTempPath(), $"codecoverage-isolation-web-{Guid.NewGuid():N}");

  #endregion

  #region Methods

  // The console never migrates its database: the suite creates the schema itself, as a deployment does.
  public async Task MigrateAsync(CancellationToken cancellationToken)
  {
    await using var scope = Services.CreateAsyncScope();

    await scope.ServiceProvider.GetRequiredService<CodeCoverageDbContext>().Database.MigrateAsync(cancellationToken);
  }

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
    builder.UseSetting(SerilogConfiguration.FilePathKey, Path.Combine(_dataPath, "logs", "codecoverage-web-.log"));
    builder.UseSetting("ConnectionStrings:CodeCoverage", connectionString);
    builder.UseSetting("CodeCoverage:Database:Provider", "PostgreSql");
    builder.UseSetting("DataProtection:AllowUnprotectedKeys", "true");
    builder.UseSetting("GitHub:AppId", "1");
    builder.UseSetting("GitHub:ClientId", "Iv1.testclient");

    builder.ConfigureTestServices(static services =>
    {
      services.AddDataProtection().UseEphemeralDataProtectionProvider();

      // The forge of the suite: every source it is asked for has the same text, and no network is reached.
      var sources = A.Fake<ISourceContentCache>();
      A.CallTo(() => sources.GetSourceAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<string>._, A<CancellationToken>._))
       .Returns(TwoOrganizationWeb.SourceText);
      services.AddScoped(_ => sources);

      // A header stands for the session cookie, as in the other host tests; the user it names is a real row.
      services.AddAuthentication(static o => o.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName)
              .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, null);
    });
  }

  #endregion
}
