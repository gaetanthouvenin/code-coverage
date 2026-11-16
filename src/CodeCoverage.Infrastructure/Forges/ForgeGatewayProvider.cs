// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeGatewayProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>Resolves the gateway of a forge among the adapters registered as keyed services (ADR-014).</summary>
/// <param name="services">The services of the current scope, where each adapter is registered under its <see cref="Forge" />.</param>
/// <remarks>
///   Each adapter registers itself as a keyed <see cref="IForgeGateway" /> under its <see cref="Forge" />, in the dependency
///   injection of its forge, so a new adapter is added without touching this provider.
/// </remarks>
internal sealed class ForgeGatewayProvider(IServiceProvider services) : IForgeGatewayProvider
{
  #region Implementation of IForgeGatewayProvider

  /// <inheritdoc />
  public IForgeGateway GetGateway(Forge forge) =>
    services.GetKeyedService<IForgeGateway>(forge)
    ?? throw new NotSupportedException($"No gateway is implemented for the forge '{forge}'.");

  #endregion
}
