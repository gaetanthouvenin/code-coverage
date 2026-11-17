// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.GitHub.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the adapters of the GitHub area: the gateways of the GitHub API and the webhook validation.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>
  ///   <see cref="GitHubAppTokenProvider" /> is a singleton: it holds one lock per installation for the whole process, as
  ///   <see cref="GitHubUserTokenProvider" /> holds one per user. They need the secret protector, registered by
  ///   <see cref="Security.DataProtectionConfiguration.AddCodeCoverageDataProtection" />. The gateway of the App is the
  ///   <see cref="IForgeGateway" /> keyed by <see cref="Forge.GitHub" />, the gateway of a user the
  ///   <see cref="IForgeUserGateway" /> keyed by it, the token of a user the <see cref="IForgeUserTokenProvider" /> keyed
  ///   by it, the addresses of its pages the <see cref="IForgeLinks" /> keyed by it, the listing of the repositories of
  ///   an installation the <see cref="IForgeDirectoryGateway" /> keyed by it and what it accepts the
  ///   <see cref="Domain.Contracts.Forges.ForgeCapabilities" /> keyed by it, which
  ///   <see cref="ForgeGatewayProvider" /> resolves.
  /// </remarks>
  private static IServiceCollection AddGitHub(this IServiceCollection services)
  {
    services.AddOptions<GitHubOptions>().BindConfiguration(GitHubOptions.SectionName).ValidateOnStart();
    services.AddMemoryCache();
    services.AddGitHubHttpClient();
    services.AddSingleton<GitHubAppTokenProvider>();
    services.AddSingleton<IWebhookSignatureValidator, WebhookSignatureValidator>();
    services.AddSingleton<ISetupTokenIssuer, SetupTokenIssuer>();
    services.AddTransient<IGitHubAppGateway>(static provider => provider.GetRequiredService<GitHubAppGateway>());
    services.AddTransient<IGitHubUserGateway>(static provider => provider.GetRequiredService<GitHubUserGateway>());
    services.AddKeyedScoped<IForgeGateway>(Forge.GitHub, static (provider, _) => provider.GetRequiredService<GitHubAppGateway>());
    services.AddKeyedScoped<IForgeUserGateway>(Forge.GitHub, static (provider, _) => provider.GetRequiredService<GitHubUserGateway>());
    services.AddKeyedSingleton<IForgeUserTokenProvider, GitHubUserTokenProvider>(Forge.GitHub);
    services.AddKeyedSingleton<IForgeLinks, GitHubLinks>(Forge.GitHub);
    services.AddKeyedTransient<IForgeDirectoryGateway, GitHubDirectoryGateway>(Forge.GitHub);
    services.AddKeyedSingleton(Forge.GitHub, GitHubCapabilities.Value);
    services.AddTransient<IForgeGatewayProvider, ForgeGatewayProvider>();
    services.AddKeyedSingleton<IForgeAvailabilityContributor, GitHubAvailabilityContributor>(Forge.GitHub);
    services.AddTransient<IForgeAvailability, ForgeAvailability>();

    return services;
  }

  #endregion
}
