// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationBuilderExtensions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>Adds the settings store to the configuration sources.</summary>
public static class ConfigurationBuilderExtensions
{
  #region Methods

  /// <summary>
  ///   Adds the settings saved from the screen as the <b>weakest</b> source, so that the priority is:
  ///   command line &gt; environment variables &gt; <c>appsettings.json</c> &gt; settings store.
  /// </summary>
  /// <param name="builder">The configuration builder.</param>
  /// <param name="connectionString">The connection string of the database.</param>
  /// <param name="provider">The database engine.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="connectionString" /> is <see langword="null" />, empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="provider" /> is not a known engine.</exception>
  /// <remarks>The source is inserted first, whatever the sources added before or after it.</remarks>
  public static IConfigurationBuilder AddCodeCoverageSettingsStore(
    this IConfigurationBuilder builder,
    string connectionString,
    DatabaseProvider provider)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    ArgumentOutOfRangeException.ThrowIfUndefined(provider);

    builder.Sources.Insert(0, new DatabaseSettingsConfigurationSource(connectionString, provider));

    return builder;
  }

  /// <summary>
  ///   Adds the settings store as the weakest source, on the database named by the bootstrap settings of
  ///   the host: <c>ConnectionStrings:CodeCoverage</c> and <c>CodeCoverage:Database:Provider</c>.
  /// </summary>
  /// <param name="configuration">The configuration of the host, being built.</param>
  /// <returns>The same configuration, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="configuration" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException"><c>CodeCoverage:Database:Provider</c> is not a known engine.</exception>
  /// <remarks>
  ///   Without a connection string, no store is added: the database context reports the missing
  ///   connection string when it is first used.
  /// </remarks>
  public static IConfigurationManager AddCodeCoverageSettingsStore(this IConfigurationManager configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      return configuration;
    }

    var provider = configuration.GetSection(DatabaseOptions.SectionName)
                                .GetValue(nameof(DatabaseOptions.Provider), DatabaseProvider.PostgreSql);

    configuration.AddCodeCoverageSettingsStore(connectionString, provider);

    return configuration;
  }

  #endregion
}
