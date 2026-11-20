// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeEventTranslator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads the deliveries of one forge with connections into neutral events.</summary>
/// <remarks>
///   Each adapter registers its translator under its forge, in <c>Infrastructure/{Forge}/Events/</c>. A translator reads
///   the stored body only: it never calls its forge.
/// </remarks>
public interface IForgeEventTranslator
{
  #region Methods

  /// <summary>Translates a delivery into the event it reports.</summary>
  /// <param name="delivery">The delivery, of the forge of this translator.</param>
  /// <returns>
  ///   The event, or <see langword="null" /> when the delivery is ignored: an event this application does not follow,
  ///   an action that changes nothing that is recorded, or a body that cannot be read.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="delivery" /> is <see langword="null" />.</exception>
  ForgeEvent? Translate(ForgeDelivery delivery);

  #endregion
}
