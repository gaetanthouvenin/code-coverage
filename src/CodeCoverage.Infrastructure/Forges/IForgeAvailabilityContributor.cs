// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeAvailabilityContributor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>Tells whether the forge of one adapter can admit organizations and sign users in.</summary>
/// <remarks>
///   Each adapter registers its contributor as a keyed service under its <see cref="Forge" />, next to its gateway, and
///   reads its settings through <c>IOptionsMonitor&lt;T&gt;</c> at each call, so a change applies without a restart.
///   A forge without a contributor is neither admitted nor offered for sign-in.
/// </remarks>
internal interface IForgeAvailabilityContributor
{
  #region Properties

  /// <summary>Gets a value indicating whether the connection settings of the forge are filled.</summary>
  /// <value><see langword="true" /> when an organization of the forge can be admitted.</value>
  bool CanAdmit { get; }

  /// <summary>Gets a value indicating whether the OAuth or OpenID Connect client of the forge is configured.</summary>
  /// <value><see langword="true" /> when a user can sign in with the forge.</value>
  bool CanSignIn { get; }

  #endregion
}
