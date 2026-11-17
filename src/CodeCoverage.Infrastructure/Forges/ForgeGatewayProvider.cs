// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeGatewayProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>Resolves the gateways of a forge among the adapters registered as keyed services (ADR-014).</summary>
/// <param name="services">The services of the current scope, where each adapter is registered under its <see cref="Forge" />.</param>
/// <remarks>
///   Each adapter registers itself as a keyed <see cref="IForgeGateway" />, as a keyed <see cref="IForgeUserGateway" />, as
///   a keyed <see cref="IForgeUserTokenProvider" />, as keyed <see cref="IForgeLinks" /> and as a keyed
///   <see cref="IForgeDirectoryGateway" />, under its <see cref="Forge" />,
///   in the dependency injection of its forge, so a new adapter is added without touching this provider.
/// </remarks>
internal sealed class ForgeGatewayProvider(IServiceProvider services) : IForgeGatewayProvider
{
  #region Implementation of IForgeGatewayProvider

  /// <inheritdoc />
  public IForgeGateway GetGateway(Forge forge) =>
    services.GetKeyedService<IForgeGateway>(forge)
    ?? throw new NotSupportedException($"No gateway is implemented for the forge '{forge}'.");

  /// <inheritdoc />
  public IForgeUserGateway GetUserGateway(Forge forge) =>
    services.GetKeyedService<IForgeUserGateway>(forge)
    ?? throw new NotSupportedException($"No user gateway is implemented for the forge '{forge}'.");

  /// <inheritdoc />
  public IForgeUserTokenProvider GetUserTokenProvider(Forge forge) =>
    services.GetKeyedService<IForgeUserTokenProvider>(forge)
    ?? throw new NotSupportedException($"No user token provider is implemented for the forge '{forge}'.");

  /// <inheritdoc />
  public IForgeLinks GetLinks(Forge forge) =>
    services.GetKeyedService<IForgeLinks>(forge)
    ?? throw new NotSupportedException($"No links are implemented for the forge '{forge}'.");

  /// <inheritdoc />
  public IForgeDirectoryGateway GetDirectory(Forge forge) =>
    services.GetKeyedService<IForgeDirectoryGateway>(forge)
    ?? throw new NotSupportedException($"No directory gateway is implemented for the forge '{forge}'.");

  #endregion
}
