// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeGatewayProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Resolves the gateway of the forge an organization belongs to.</summary>
public interface IForgeGatewayProvider
{
  #region Methods

  /// <summary>Gets the gateway of a forge.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The gateway of the forge.</returns>
  /// <exception cref="NotSupportedException">No gateway is implemented for <paramref name="forge" />.</exception>
  IForgeGateway GetGateway(Forge forge);

  #endregion
}
