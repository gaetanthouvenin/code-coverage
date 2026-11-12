// ------------------------------------------------------------------------------------------------
// <copyright file="IOrganizationSettingRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Ports;

/// <summary>The settings of the organization of the scope that differ from the catalog (<c>OrganizationSettings</c>).</summary>
/// <remarks>
///   Every read and write goes through the organization of the scope: no method takes an organization. A setting without
///   a row has the default value of the catalog.
/// </remarks>
public interface IOrganizationSettingRepository
{
  #region Methods

  /// <summary>Lists the values stored for the organization of the scope, for reading only.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The stored values, by key, with <see cref="SettingSource.Store" /> as their source.</returns>
  Task<IReadOnlyList<SettingValue>> ListAsync(CancellationToken cancellationToken);

  /// <summary>Stores the value of a setting of the organization of the scope, saved at once.</summary>
  /// <param name="definition">The setting, of the scope of an organization.</param>
  /// <param name="value">The value, normalized.</param>
  /// <param name="updatedBy">Who changes it, as the audit trail names it.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the value is saved.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  Task SaveAsync(SettingDefinition definition, string value, string updatedBy, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Removes the value of a setting of the organization of the scope, which goes back to its default.</summary>
  /// <param name="definition">The setting.</param>
  /// <param name="cancellationToken">A token to cancel the deletion.</param>
  /// <returns><see langword="true" /> when a value was removed; <see langword="false" /> when the setting had none.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="definition" /> is <see langword="null" />.</exception>
  Task<bool> RemoveAsync(SettingDefinition definition, CancellationToken cancellationToken);

  #endregion
}
