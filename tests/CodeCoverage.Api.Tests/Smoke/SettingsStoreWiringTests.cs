// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsStoreWiringTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Api.Tests.Smoke;

// The database of the factory refuses every connection: the store source loads empty and the host starts.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class SettingsStoreWiringTests(CodeCoverageApiFactory factory)
{
  #region Methods

  // The weakest source: command line, environment variables and appsettings.json all win over it.
  [Fact]
  public void Configuration_ShouldHaveTheSettingsStoreAsWeakestSource()
  {
    var configuration = factory.Services.GetRequiredService<IConfiguration>().ShouldBeAssignableTo<IConfigurationRoot>();

    configuration.Providers.First().ShouldBeOfType<DatabaseSettingsConfigurationProvider>();
    configuration.Providers.OfType<DatabaseSettingsConfigurationProvider>().Count().ShouldBe(1);
  }

  [Fact]
  public async Task Services_ShouldResolveTheSettingsStorePerScope()
  {
    await using var scope = factory.Services.CreateAsyncScope();

    scope.ServiceProvider.GetRequiredService<ISettingsStore>().ShouldNotBeNull();
  }

  // Both hosts poll the settings revision, so that a change saved on one reaches the other.
  [Fact]
  public void Host_ShouldRegisterTheSettingsChangeWatcher()
  {
    var hostedServices = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name);

    hostedServices.ShouldContain("SettingsChangeWatcher");
  }

  // Only the Api runs the background jobs; the factory disables them (Worker:Enabled=false) since its
  // database refuses every connection.
  [Fact]
  public void Host_ShouldRegisterTheJobWorkerAndTheLeaseSweeper()
  {
    var hostedServices = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name).ToList();

    hostedServices.ShouldContain("JobWorker");
    hostedServices.ShouldContain("LeaseSweeper");
    hostedServices.ShouldContain("RecurringJobScheduler");
    factory.Services.GetRequiredService<IConfiguration>()["Worker:Enabled"].ShouldBe("false");
  }

  #endregion
}
