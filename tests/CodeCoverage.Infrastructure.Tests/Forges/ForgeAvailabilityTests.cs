// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeAvailabilityTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Tests.Forges;

public sealed class ForgeAvailabilityTests
{
  #region Fields

  private readonly GitHubOptions _gitHub = new();

  #endregion

  #region Methods

  [Fact]
  public void AdmissionForges_GitHubAppConfigured_ShouldListGitHub()
  {
    _gitHub.AppId = "123456";

    using var provider = BuildProvider();

    Availability(provider).AdmissionForges.ShouldBe([Forge.GitHub]);
  }

  [Fact]
  public void AdmissionForges_AppIdMissing_ShouldBeEmpty()
  {
    _gitHub.ClientId = "Iv1.client";

    using var provider = BuildProvider();

    Availability(provider).AdmissionForges.ShouldBeEmpty();
  }

  [Fact]
  public void SignInForges_ClientIdMissing_ShouldBeEmpty()
  {
    _gitHub.AppId = "123456";

    using var provider = BuildProvider();

    Availability(provider).SignInForges.ShouldBeEmpty();
  }

  // The acceptance of M7.05: with GitHub alone and configured, both lists hold GitHub.
  [Fact]
  public void AdmissionAndSignInForges_GitHubAloneConfigured_ShouldBothListGitHub()
  {
    _gitHub.AppId = "123456";
    _gitHub.ClientId = "Iv1.client";

    using var provider = BuildProvider();
    var availability = Availability(provider);

    availability.AdmissionForges.ShouldBe([Forge.GitHub]);
    availability.SignInForges.ShouldBe([Forge.GitHub]);
  }

  // An adapter registered before its Web sign-in admits organizations but offers no sign-in button.
  [Fact]
  public void SignInForges_ContributorWithoutSignIn_ShouldLeaveItsForgeOut()
  {
    _gitHub.AppId = "123456";
    _gitHub.ClientId = "Iv1.client";
    var azureDevOps = A.Fake<IForgeAvailabilityContributor>();
    A.CallTo(() => azureDevOps.CanAdmit).Returns(true);
    A.CallTo(() => azureDevOps.CanSignIn).Returns(false);

    using var provider = BuildProvider(services => services.AddKeyedSingleton(Forge.AzureDevOps, azureDevOps));
    var availability = Availability(provider);

    availability.AdmissionForges.ShouldBe([Forge.GitHub, Forge.AzureDevOps]);
    availability.SignInForges.ShouldBe([Forge.GitHub]);
  }

  // The acceptance of M7.05: a change of setting applies without a restart.
  [Fact]
  public async Task AdmissionAndSignInForges_SettingChanged_ShouldApplyWithoutRestart()
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(configuration)
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider();
    var availability = services.GetRequiredService<IForgeAvailability>();

    availability.AdmissionForges.ShouldBeEmpty();
    availability.SignInForges.ShouldBeEmpty();

    configuration[SettingDefinitions.GitHubAppId] = "123456";
    configuration[SettingDefinitions.GitHubClientId] = "Iv1.client";
    configuration.Reload();

    availability.AdmissionForges.ShouldBe([Forge.GitHub]);
    availability.SignInForges.ShouldBe([Forge.GitHub]);
  }

  // M7.39: the Entra ID application admits Azure DevOps organizations once its client identifier and a secret are
  // filled, without a restart; the sign-in with Entra ID is not offered yet.
  [Fact]
  public async Task AdmissionForges_AzureDevOpsApplicationConfigured_ShouldListAzureDevOps()
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(configuration)
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider();
    var availability = services.GetRequiredService<IForgeAvailability>();

    configuration[SettingDefinitions.AzureDevOpsClientId] = "11111111-2222-3333-4444-555555555555";
    configuration.Reload();

    availability.AdmissionForges.ShouldBeEmpty();

    configuration[SettingDefinitions.AzureDevOpsClientSecret] = "protected-secret";
    configuration.Reload();

    availability.AdmissionForges.ShouldBe([Forge.AzureDevOps]);
    availability.SignInForges.ShouldBeEmpty();
  }

  private static IForgeAvailability Availability(ServiceProvider provider)
  {
    return new ForgeAvailability(provider);
  }

  private ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
  {
    var options = A.Fake<IOptionsMonitor<GitHubOptions>>();
    A.CallTo(() => options.CurrentValue).Returns(_gitHub);

    var services = new ServiceCollection();
    services.AddKeyedSingleton<IForgeAvailabilityContributor>(Forge.GitHub, new GitHubAvailabilityContributor(options));
    configure?.Invoke(services);

    return services.BuildServiceProvider(validateScopes: true);
  }

  #endregion
}
