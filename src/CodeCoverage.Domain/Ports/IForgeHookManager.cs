// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeHookManager.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Ports;

/// <summary>
///   Creates and checks, through the API of a forge with connections, the hooks that deliver the events of a connection to
///   its route; registered under its forge and resolved by <see cref="IForgeGatewayProvider.GetHookManager" />.
/// </summary>
/// <remarks>
///   <para>
///     A refusal of the forge is reported as <see cref="ForgeHookState.Forbidden" /> for its scope, never thrown: a scope
///     without the right to a hook never fails the others, nor the job. Only a forge that cannot be reached, or a
///     cancellation, throws.
///   </para>
///   <para>GitHub has no hook manager: the App delivers the events of its installations (ADR-017).</para>
/// </remarks>
public interface IForgeHookManager
{
  #region Properties

  /// <summary>Gets what each hook of the forge covers: the account, a project or a repository.</summary>
  ForgeHookScopeKind ScopeKind { get; }

  #endregion

  #region Methods

  /// <summary>Creates the hooks the scopes miss, and brings the existing ones to the callback.</summary>
  /// <param name="connection">The connection, whose credential calls the forge.</param>
  /// <param name="scopes">The scopes, all of <see cref="ScopeKind" />, with the hook last known for each.</param>
  /// <param name="callback">The address the hooks deliver to and the current webhook secret of the connection.</param>
  /// <param name="cancellationToken">A token to cancel the calls.</param>
  /// <returns>The state of the hook of each scope, once ensured.</returns>
  /// <remarks>
  ///   A <see cref="ForgeHookScopeKind.Project" /> scope carries the name of the project as tracked, which the manager
  ///   resolves to the project of the forge. A scope without a hook last known may already have one at the forge, after a
  ///   rename of its project or a lost row: the manager reuses a hook of the scope that delivers to the callback before it
  ///   creates one, so that a project never gets a second hook.
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="connection" />, <paramref name="scopes" /> or <paramref name="callback" /> is <see langword="null" />.
  /// </exception>
  Task<IReadOnlyList<ForgeHookResult>> EnsureAsync(
    ForgeConnectionRef connection,
    IReadOnlyList<ForgeHookScope> scopes,
    ForgeHookCallback callback,
    CancellationToken cancellationToken);

  /// <summary>Reads the state of the hooks of the scopes, without changing anything at the forge.</summary>
  /// <param name="connection">The connection, whose credential calls the forge.</param>
  /// <param name="scopes">The scopes, all of <see cref="ScopeKind" />, with the hook last known for each.</param>
  /// <param name="cancellationToken">A token to cancel the calls.</param>
  /// <returns>The state of the hook of each scope.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> or <paramref name="scopes" /> is <see langword="null" />.</exception>
  Task<IReadOnlyList<ForgeHookResult>> CheckAsync(ForgeConnectionRef connection, IReadOnlyList<ForgeHookScope> scopes, CancellationToken cancellationToken);

  #endregion
}
