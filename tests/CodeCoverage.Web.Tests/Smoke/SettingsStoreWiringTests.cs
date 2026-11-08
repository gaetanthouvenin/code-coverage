// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsStoreWiringTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Smoke;

// The database of the factory refuses every connection: the store source loads empty and the host starts.
[Collection(CodeCoverageWebCollection.Name)]
public sealed class SettingsStoreWiringTests(CodeCoverageWebFactory factory)
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

  // The Web queues jobs but never runs them: only the Api registers the worker (PLAN M0.24).
  [Fact]
  public void Host_ShouldNeverRegisterTheJobWorker()
  {
    var hostedServices = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name).ToList();

    hostedServices.ShouldNotContain("JobWorker");
    hostedServices.ShouldNotContain("LeaseSweeper");
    hostedServices.ShouldNotContain("RecurringJobScheduler");
  }

  #endregion
}
