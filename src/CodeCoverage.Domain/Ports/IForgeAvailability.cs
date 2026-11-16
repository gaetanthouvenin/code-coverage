// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeAvailability.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Tells, forge by forge, whether an organization can be admitted and whether a user can sign in.</summary>
/// <remarks>
///   The two lists are distinct: an adapter registered before its Web sign-in admits organizations without offering a
///   sign-in button. Both follow the settings without a restart.
/// </remarks>
public interface IForgeAvailability
{
  #region Properties

  /// <summary>Gets the forges whose adapter is registered and whose connection settings are filled.</summary>
  /// <value>The forges an organization can be admitted on, in the order of <see cref="Forge" />; empty when none is.</value>
  IReadOnlyList<Forge> AdmissionForges { get; }

  /// <summary>Gets the forges whose OAuth or OpenID Connect client is configured.</summary>
  /// <value>The forges a user can sign in with, in the order of <see cref="Forge" />; empty when none is.</value>
  IReadOnlyList<Forge> SignInForges { get; }

  #endregion
}
