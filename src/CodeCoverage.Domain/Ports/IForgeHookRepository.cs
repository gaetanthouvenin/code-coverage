// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeHookRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;

namespace CodeCoverage.Domain.Ports;

/// <summary>The hooks the forge connections of the organization of the scope created at their forge (<c>ForgeHooks</c>).</summary>
/// <remarks>
///   Every read and write goes through the organization of the scope: no method takes an organization, and a
///   connection of another organization has no hook here.
/// </remarks>
public interface IForgeHookRepository
{
  #region Methods

  /// <summary>Lists the hooks of a connection, for reading only.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The hooks of the connection, by kind and identifier of their scope; empty for a connection of another organization.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  Task<IReadOnlyList<ForgeHook>> ListAsync(string connectionId, CancellationToken cancellationToken);

  /// <summary>Stores the state of the hook of a scope, saved at once.</summary>
  /// <param name="hook">
  ///   The hook; when its connection already has a hook of the same scope, the state of that hook is replaced, and no
  ///   hook is added.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the hook is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="hook" /> is <see langword="null" />.</exception>
  Task UpsertAsync(ForgeHook hook, CancellationToken cancellationToken);

  #endregion
}
