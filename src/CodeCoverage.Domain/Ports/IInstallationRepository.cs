// ------------------------------------------------------------------------------------------------
// <copyright file="IInstallationRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Ports;

/// <summary>The installations of the GitHub App.</summary>
/// <remarks>
///   The port reads a global table without business data: it serves before a scope enters an organization, to
///   find the one of a webhook.
/// </remarks>
public interface IInstallationRepository
{
  #region Methods

  /// <summary>Finds an installation by the identifier GitHub gives it.</summary>
  /// <param name="id">The identifier of the installation.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The installation, or <see langword="null" />.</returns>
  Task<Installation?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Finds the organization of an installation and its state, in one read.</summary>
  /// <param name="id">The identifier of the installation.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The owner of the installation, or <see langword="null" /> when the installation is unknown.</returns>
  Task<InstallationOwner?> FindOwnerAsync(long id, CancellationToken cancellationToken);

  /// <summary>Lists the installations recorded for an organization, the most recent first.</summary>
  /// <param name="organizationId">The identifier of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The identifiers of its installations, the highest first: GitHub gives them in increasing order, whatever the
  ///   order they were recorded in here.
  /// </returns>
  /// <remarks>The rows of the installations GitHub deleted are kept: this tells which ones existed, not which ones still do.</remarks>
  Task<IReadOnlyList<long>> ListIdsForOrganizationAsync(long organizationId, CancellationToken cancellationToken);

  /// <summary>Adds an installation, saved at once.</summary>
  /// <param name="installation">The installation.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the installation is saved.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when a concurrent save added it: reading again finds its row.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="installation" /> is <see langword="null" />.</exception>
  Task AddAsync(Installation installation, CancellationToken cancellationToken);

  #endregion
}
