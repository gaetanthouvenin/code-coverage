// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationSourceInspector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>Tells which configuration source provides the effective value of a key.</summary>
/// <remarks>
///   <para>
///     The providers are asked from the strongest (the last one added) to the weakest, as
///     <see cref="IConfigurationRoot" /> does: the first one that holds the key gives the effective
///     value. <see cref="DatabaseSettingsConfigurationProvider" /> is
///     <see cref="SettingSource.Store" />; any other provider (command line, environment variables,
///     <c>appsettings.json</c>) is <see cref="SettingSource.Environment" />, read-only on screen.
///   </para>
///   <para>
///     A configuration that is not a root cannot tell its sources apart: a value it holds is taken as
///     coming from <see cref="SettingSource.Environment" />, so that it is never overwritten from the
///     screen.
///   </para>
/// </remarks>
internal static class ConfigurationSourceInspector
{
  #region Methods

  /// <summary>Finds the effective value of a key and its source.</summary>
  /// <param name="configuration">The configuration of the host.</param>
  /// <param name="key">The configuration key.</param>
  /// <returns>
  ///   The source and the value of the strongest provider holding the key, or
  ///   <see cref="SettingSource.Default" /> and <see langword="null" /> when none holds a value.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="configuration" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="key" /> is <see langword="null" />, empty or white space.</exception>
  public static (SettingSource Source, string? Value) Resolve(IConfiguration configuration, string key)
  {
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentException.ThrowIfNullOrWhiteSpace(key);

    if (configuration is not IConfigurationRoot root)
    {
      return configuration[key] is { } value ? (SettingSource.Environment, value) : (SettingSource.Default, null);
    }

    foreach (var provider in root.Providers.Reverse())
    {
      if (!provider.TryGet(key, out var value))
      {
        continue;
      }

      if (value is null)
      {
        break;
      }

      return provider is DatabaseSettingsConfigurationProvider
               ? (SettingSource.Store, value)
               : (SettingSource.Environment, value);
    }

    return (SettingSource.Default, null);
  }

  #endregion
}
