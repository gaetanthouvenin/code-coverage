// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeGatewayProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Resolves the gateways of the forge an organization or a user belongs to.</summary>
public interface IForgeGatewayProvider
{
  #region Methods

  /// <summary>Gets the gateway of a forge.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The gateway of the forge.</returns>
  /// <exception cref="NotSupportedException">No gateway is implemented for <paramref name="forge" />.</exception>
  IForgeGateway GetGateway(Forge forge);

  /// <summary>Gets the gateway that calls a forge in the name of a user.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The user gateway of the forge.</returns>
  /// <exception cref="NotSupportedException">No user gateway is implemented for <paramref name="forge" />.</exception>
  IForgeUserGateway GetUserGateway(Forge forge);

  /// <summary>Gets what gives the token a forge issued for a user, renewed when it is about to expire.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The user token provider of the forge.</returns>
  /// <exception cref="NotSupportedException">No user token provider is implemented for <paramref name="forge" />.</exception>
  IForgeUserTokenProvider GetUserTokenProvider(Forge forge);

  #endregion
}
