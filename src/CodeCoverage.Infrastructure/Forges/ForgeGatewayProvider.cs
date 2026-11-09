// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeGatewayProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>Resolves the gateway of a forge; GitHub is the only forge implemented so far (ADR-014).</summary>
/// <param name="gitHub">The gateway of GitHub.</param>
internal sealed class ForgeGatewayProvider(GitHubAppGateway gitHub) : IForgeGatewayProvider
{
  #region Implementation of IForgeGatewayProvider

  /// <inheritdoc />
  public IForgeGateway GetGateway(Forge forge) =>
    forge is Forge.GitHub ? gitHub : throw new NotSupportedException($"No gateway is implemented for the forge '{forge}'.");

  #endregion
}
