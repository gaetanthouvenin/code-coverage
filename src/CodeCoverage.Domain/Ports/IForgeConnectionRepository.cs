// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeConnectionRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;

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

  /// <summary>
  ///   Adds an organization, its connection and the jobs that follow them, saved together in one transaction or not at
  ///   all (M7.12a).
  /// </summary>
  /// <param name="organization">The organization, not saved yet.</param>
  /// <param name="connection">
  ///   Builds the connection once the organization has its identifier: the connection must name that organization and
  ///   its forge.
  /// </param>
  /// <param name="jobs">
  ///   Builds the jobs once the organization has its identifier, such as the synchronization of the repositories of the
  ///   connection and the refresh of the memberships of the administrator. Each one names that organization or runs on
  ///   the lane of the instance.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The connection, saved with its organization and its jobs.</returns>
  /// <remarks>
  ///   <para>
  ///     The organization takes its identifier from the database, so the connection and the jobs, which name it, are
  ///     built inside the transaction. No connection is saved without its organization, nor the reverse.
  ///   </para>
  ///   <para>
  ///     Refused as a conflict (<c>General.ConcurrentChange</c>) when an organization of that account or login, or a
  ///     connection of that identifier, exists. The admission of a GitHub organization keeps
  ///     <see cref="IOrganizationDirectory.UpsertFromForgeAsync" />.
  ///   </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="organization" />, <paramref name="connection" /> or <paramref name="jobs" /> is
  ///   <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException"><paramref name="organization" /> is already saved.</exception>
  /// <exception cref="InvalidOperationException">
  ///   The connection names another organization or forge, a secret of the connection is not protected, or a job
  ///   names another organization or takes the organization of the scope.
  /// </exception>
  Task<ForgeConnection> AddWithOrganizationAsync(
    Organization organization,
    Func<Organization, ForgeConnection> connection,
    Func<Organization, IReadOnlyList<JobRequest>> jobs,
    CancellationToken cancellationToken);

  /// <summary>Saves the changes of a connection read in this scope.</summary>
  /// <param name="connection">The connection.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the connection is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The connection was not read in this scope.</exception>
  Task UpdateAsync(ForgeConnection connection, CancellationToken cancellationToken);

  #endregion
}
