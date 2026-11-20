// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeWebhookValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Webhooks;

namespace CodeCoverage.Domain.Ports;

/// <summary>Authenticates a webhook delivery of a forge with connections by the secret of its connection (ADR-017).</summary>
/// <remarks>
///   <para>
///     Registered as a service keyed by its <see cref="Organizations.Forge" /> and resolved by
///     <see cref="IForgeGatewayProvider.GetWebhookValidator" />: each forge reads its own headers, such as a signature
///     or a shared secret, and compares them in constant time. Nothing is called on the forge.
///   </para>
///   <para>
///     GitHub keeps <see cref="IWebhookSignatureValidator" /> and the secret of its App for its installations: it has
///     no connection.
///   </para>
/// </remarks>
public interface IForgeWebhookValidator
{
  #region Methods

  /// <summary>Checks a delivery against the secrets of its connection.</summary>
  /// <param name="request">The delivery, of the forge of this validator.</param>
  /// <param name="secrets">The decrypted webhook secrets of the connection: the current one, then the previous one, if any.</param>
  /// <returns><see langword="true" /> when one of the secrets authenticates the delivery.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> or <paramref name="secrets" /> is <see langword="null" />.</exception>
  bool IsValid(ForgeWebhookRequest request, IReadOnlyList<string> secrets);

  #endregion
}
