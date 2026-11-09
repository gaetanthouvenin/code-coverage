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

  /// <summary>Adds an installation, saved at once.</summary>
  /// <param name="installation">The installation.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the installation is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="installation" /> is <see langword="null" />.</exception>
  Task AddAsync(Installation installation, CancellationToken cancellationToken);

  #endregion
}
