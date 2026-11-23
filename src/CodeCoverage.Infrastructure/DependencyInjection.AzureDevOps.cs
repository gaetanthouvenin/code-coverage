// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.AzureDevOps.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.AzureDevOps;
using CodeCoverage.Infrastructure.AzureDevOps.Events;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>
  ///   Registers the adapters of the Azure DevOps area: the settings of the Microsoft Entra ID application, the HTTP
  ///   clients, the tokens of the application, its gateway, the gateway and the tokens of a user, its availability, the
  ///   translator of its service hooks and their validator.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>
  ///   <see cref="ForgeConnectionCredentialReader" /> and <see cref="AzureDevOpsTokenProvider" /> are scoped: they read
  ///   the organization of the scope. The reader is shared with the adapters of the next forges, which register it
  ///   the same way. The gateway is the <see cref="IForgeGateway" /> keyed by <see cref="Forge.AzureDevOps" />, the
  ///   gateway of a user the <see cref="IForgeUserGateway" /> keyed by it and the delegated token of a user the
  ///   <see cref="IForgeUserTokenProvider" /> keyed by it, which <see cref="ForgeGatewayProvider" /> resolves.
  ///   <see cref="AzureDevOpsUserTokenProvider" /> is a singleton: it holds one lock per user for the whole process. The
  ///   availability is the <see cref="IForgeAvailabilityContributor" /> keyed by <see cref="Forge.AzureDevOps" />, the translator of its service hooks the <see cref="IForgeEventTranslator" /> keyed by it, and the validator of their secret header the <see cref="IForgeWebhookValidator" /> keyed by it. They need the secret protector, registered by
  ///   <see cref="Security.DataProtectionConfiguration.AddCodeCoverageDataProtection" />.
  /// </remarks>
  private static IServiceCollection AddAzureDevOps(this IServiceCollection services)
  {
    services.AddOptions<AzureDevOpsOptions>().BindConfiguration(AzureDevOpsOptions.SectionName);
    services.AddMemoryCache();
    services.AddAzureDevOpsHttpClient();
    services.TryAddScoped<ForgeConnectionCredentialReader>();
    services.AddScoped<AzureDevOpsTokenProvider>();
    services.AddKeyedScoped<IForgeGateway>(Forge.AzureDevOps, static (provider, _) => provider.GetRequiredService<AzureDevOpsGateway>());
    services.AddKeyedScoped<IForgeUserGateway>(Forge.AzureDevOps, static (provider, _) => provider.GetRequiredService<AzureDevOpsUserGateway>());
    services.AddKeyedSingleton<IForgeUserTokenProvider, AzureDevOpsUserTokenProvider>(Forge.AzureDevOps);
    services.AddKeyedSingleton<IForgeAvailabilityContributor, AzureDevOpsAvailabilityContributor>(Forge.AzureDevOps);
    services.AddKeyedSingleton<IForgeEventTranslator, AzureDevOpsEventTranslator>(Forge.AzureDevOps);
    services.AddKeyedSingleton<IForgeWebhookValidator, AzureDevOpsWebhookValidator>(Forge.AzureDevOps);

    return services;
  }

  #endregion
}
