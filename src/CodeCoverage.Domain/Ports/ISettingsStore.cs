// ------------------------------------------------------------------------------------------------
// <copyright file="ISettingsStore.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Ports;

/// <summary>
///   The settings of the instance: reads the effective value of a setting with its source, and saves
///   the values changed from the screen or the API in the settings store, where the running
///   configuration of every host picks them up.
/// </summary>
/// <remarks>
///   <para>
///     The priority is: command line, then environment variables, then <c>appsettings.json</c>, then
///     the store, then the default of <see cref="SettingDefinitions" />.
///   </para>
///   <para>
///     A save is applied at once by the host that makes it, and within a few seconds by the others.
///     The author of a change is the principal of the current scope (<see cref="ICurrentUser" />):
///     the caller never names it.
///   </para>
///   <para>
///     The store never protects a secret itself: the caller passes the value already protected by
///     <see cref="ISecretProtector" />, and reads it back in that form.
///   </para>
/// </remarks>
public interface ISettingsStore
{
  #region Methods

  /// <summary>Reads the effective value of a setting and where it comes from.</summary>
  /// <param name="definition">The setting, from <see cref="SettingDefinitions" />.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The effective value.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="definition" /> is <see langword="null" />.</exception>
  Task<SettingValue> GetAsync(SettingDefinition definition, CancellationToken cancellationToken);

  /// <summary>Reads the effective value of every setting of the catalog.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The effective values, in the order of <see cref="SettingDefinitions.All" />.</returns>
  Task<IReadOnlyList<SettingValue>> ListAsync(CancellationToken cancellationToken);

  /// <summary>
  ///   Saves a value in the store, saved at once with the increment of the settings revision, in the
  ///   same transaction.
  /// </summary>
  /// <param name="definition">The setting, from <see cref="SettingDefinitions" />.</param>
  /// <param name="value">
  ///   The value, written with the invariant culture and already validated; the protected form for a
  ///   secret.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes once the value is saved and applied to the running configuration.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="definition" /> or <paramref name="value" /> is <see langword="null" />.
  /// </exception>
  Task SaveAsync(SettingDefinition definition, string value, CancellationToken cancellationToken);

  /// <summary>
  ///   Removes the value saved in the store, so that the default applies again; saved at once with the
  ///   increment of the settings revision, in the same transaction. Does nothing when the store holds
  ///   no value.
  /// </summary>
  /// <param name="definition">The setting, from <see cref="SettingDefinitions" />.</param>
  /// <param name="cancellationToken">A token to cancel the removal.</param>
  /// <returns>A task that completes once the value is removed and the change applied.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="definition" /> is <see langword="null" />.</exception>
  Task ResetAsync(SettingDefinition definition, CancellationToken cancellationToken);

  #endregion
}
