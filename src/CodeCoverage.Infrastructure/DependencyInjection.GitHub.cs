// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.GitHub.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

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
  ///   <see cref="GitHubAppTokenProvider" /> is a singleton: it holds one lock per installation for the whole process.
  ///   It needs the secret protector, registered by
  ///   <see cref="Security.DataProtectionConfiguration.AddCodeCoverageDataProtection" />.
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
    services.AddSingleton<IGitHubUserTokenProvider, GitHubUserTokenProvider>();
    services.AddTransient<IForgeGatewayProvider, ForgeGatewayProvider>();

    return services;
  }

  #endregion
}
