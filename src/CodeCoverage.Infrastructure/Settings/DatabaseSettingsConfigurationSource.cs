// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseSettingsConfigurationSource.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>Configuration source over the <c>Settings</c> table of the settings store.</summary>
/// <param name="connectionString">The connection string of the database.</param>
/// <param name="provider">The database engine.</param>
/// <remarks>Added as the weakest source by <see cref="ConfigurationBuilderExtensions.AddCodeCoverageSettingsStore(IConfigurationBuilder, string, DatabaseProvider)" />.</remarks>
public sealed class DatabaseSettingsConfigurationSource(string connectionString, DatabaseProvider provider)
  : IConfigurationSource
{
  #region Properties

  /// <summary>Gets the connection string of the database.</summary>
  public string ConnectionString { get; } = connectionString;

  /// <summary>Gets the database engine.</summary>
  public DatabaseProvider Provider { get; } = provider;

  #endregion

  #region Implementation of IConfigurationSource

  /// <inheritdoc />
  public IConfigurationProvider Build(IConfigurationBuilder builder)
  {
    return new DatabaseSettingsConfigurationProvider(ConnectionString, Provider);
  }

  #endregion
}
