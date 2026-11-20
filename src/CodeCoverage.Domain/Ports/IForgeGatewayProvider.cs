// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeGatewayProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
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

  /// <summary>Gets the builder of the addresses of the pages of a forge.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The links of the forge.</returns>
  /// <exception cref="NotSupportedException">No links are implemented for <paramref name="forge" />.</exception>
  IForgeLinks GetLinks(Forge forge);

  /// <summary>Gets what gives the token a forge issued for a user, renewed when it is about to expire.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The user token provider of the forge.</returns>
  /// <exception cref="NotSupportedException">No user token provider is implemented for <paramref name="forge" />.</exception>
  IForgeUserTokenProvider GetUserTokenProvider(Forge forge);

  /// <summary>Gets the gateway that reads the account and the repositories a connection of a forge serves.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The directory gateway of the forge.</returns>
  /// <exception cref="NotSupportedException">No directory gateway is implemented for <paramref name="forge" />.</exception>
  IForgeDirectoryGateway GetDirectory(Forge forge);

  /// <summary>Gets what verifies, by a light call to a forge, that a connection reaches the account it names.</summary>
  /// <param name="forge">The forge, never GitHub, whose installations are not connections.</param>
  /// <returns>The connection verifier of the forge.</returns>
  /// <exception cref="NotSupportedException">No connection verifier is implemented for <paramref name="forge" />.</exception>
  IForgeConnectionVerifier GetVerifier(Forge forge);

  /// <summary>Gets what a forge accepts in what this application writes on it, such as the longest comment.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The capabilities of the forge.</returns>
  /// <exception cref="NotSupportedException">No capabilities are implemented for <paramref name="forge" />.</exception>
  ForgeCapabilities GetCapabilities(Forge forge);

  /// <summary>Gets what reads the deliveries of a forge with connections into neutral events.</summary>
  /// <param name="forge">The forge, never GitHub, whose deliveries keep their own use case.</param>
  /// <returns>The event translator of the forge.</returns>
  /// <exception cref="NotSupportedException">No event translator is implemented for <paramref name="forge" />.</exception>
  IForgeEventTranslator GetEventTranslator(Forge forge);

  /// <summary>Gets what authenticates the webhook deliveries of a forge with connections by the secret of their connection.</summary>
  /// <param name="forge">The forge, never GitHub, whose deliveries keep the secret of its App.</param>
  /// <returns>The webhook validator of the forge.</returns>
  /// <exception cref="NotSupportedException">No webhook validator is implemented for <paramref name="forge" />.</exception>
  IForgeWebhookValidator GetWebhookValidator(Forge forge);

  #endregion
}
