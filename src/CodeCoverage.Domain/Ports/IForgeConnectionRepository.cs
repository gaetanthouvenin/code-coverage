// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeConnectionRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;

namespace CodeCoverage.Domain.Ports;

/// <summary>The connections of the organizations to the forges other than GitHub (ADR-017).</summary>
/// <remarks>
///   <para>
///     The port reads a global table, under no query filter: it serves before a scope enters an organization, to find
///     the one of a webhook, and serves the coordinators that queue one child per organization. Each such reader is a
///     named door of the isolation tests.
///   </para>
///   <para>The secrets of a connection are stored as they come, protected by <c>ISecretProtector</c>, never in clear.</para>
/// </remarks>
public interface IForgeConnectionRepository
{
  #region Methods

  /// <summary>Finds the organization of a connection and their states, in one read.</summary>
  /// <param name="id">The identifier of the connection.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The owner of the connection, or <see langword="null" /> when the connection is unknown.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="id" /> is <see langword="null" />.</exception>
  Task<ForgeConnectionOwner?> FindOwnerAsync(string id, CancellationToken cancellationToken);

  /// <summary>Finds a connection, tracked so that a change of it can be saved.</summary>
  /// <param name="id">The identifier of the connection.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The connection, whatever its organization, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="id" /> is <see langword="null" />.</exception>
  Task<ForgeConnection?> FindAsync(string id, CancellationToken cancellationToken);

  /// <summary>Lists the connections of an organization, suspended ones included, by identifier.</summary>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The connections of that organization only.</returns>
  Task<IReadOnlyList<ForgeConnection>> ListForOrganizationAsync(long organizationId, CancellationToken cancellationToken);

  /// <summary>Lists the connections that are not suspended, of every organization, by identifier.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The connections whose status is not <see cref="ForgeConnectionStatus.Suspended" />.</returns>
  /// <remarks>Failing and expiring connections are listed: a coordinator still checks and synchronizes them.</remarks>
  Task<IReadOnlyList<ForgeConnection>> ListActiveAsync(CancellationToken cancellationToken);

  /// <summary>Adds a connection, saved at once.</summary>
  /// <param name="connection">The connection, not saved yet.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the connection is saved.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when a connection of that identifier exists.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> is <see langword="null" />.</exception>
  Task AddAsync(ForgeConnection connection, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a connection read in this scope.</summary>
  /// <param name="connection">The connection.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the connection is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The connection was not read in this scope.</exception>
  Task UpdateAsync(ForgeConnection connection, CancellationToken cancellationToken);

  #endregion
}
