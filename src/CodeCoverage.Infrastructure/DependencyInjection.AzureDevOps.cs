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
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>
  ///   Registers the adapters of the Azure DevOps area: the settings of the Microsoft Entra ID application, the HTTP
  ///   clients, the tokens of the application, its gateway and its availability.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>
  ///   <see cref="ForgeConnectionCredentialReader" /> and <see cref="AzureDevOpsTokenProvider" /> are scoped: they read
  ///   the organization of the scope. The reader is shared with the adapters of the next forges, which register it
  ///   the same way. The gateway is the <see cref="IForgeGateway" /> keyed by <see cref="Forge.AzureDevOps" />, which
  ///   <see cref="ForgeGatewayProvider" /> resolves. The availability is the <see cref="IForgeAvailabilityContributor" /> keyed by
  ///   <see cref="Forge.AzureDevOps" />. They need the secret protector, registered by
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
    services.AddKeyedSingleton<IForgeAvailabilityContributor, AzureDevOpsAvailabilityContributor>(Forge.AzureDevOps);

    return services;
  }

  #endregion
}
