// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationSourceInspectorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Infrastructure.Tests.Settings;

public sealed class ConfigurationSourceInspectorTests
{
  #region Fields

  // Refuses every connection at once: the store starts empty and its values are set by the tests.
  private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=codecoverage;Timeout=1";

  private const string Key = "Uploads:MaxRequestMegabytes";

  #endregion

  #region Methods

  [Fact]
  public void Resolve_WhenStrongerSourceProvidesKey_ShouldReturnEnvironmentAndItsValue()
  {
    var configuration = Build(store: "100", stronger: "300");

    ConfigurationSourceInspector.Resolve(configuration, Key).ShouldBe((SettingSource.Environment, "300"));
  }

  [Fact]
  public void Resolve_WhenOnlyStoreProvidesKey_ShouldReturnStoreAndItsValue()
  {
    var configuration = Build(store: "100", stronger: null);

    ConfigurationSourceInspector.Resolve(configuration, "uploads:maxrequestmegabytes").ShouldBe((SettingSource.Store, "100"));
  }

  [Fact]
  public void Resolve_WhenNoSourceProvidesKey_ShouldReturnDefault()
  {
    var configuration = Build(store: null, stronger: null);

    ConfigurationSourceInspector.Resolve(configuration, Key).ShouldBe((SettingSource.Default, null));
  }

  // A section cannot tell its sources apart: its value is never taken as editable.
  [Fact]
  public void Resolve_OnConfigurationThatIsNotRoot_ShouldTreatValueAsEnvironment()
  {
    var section = Build(store: "100", stronger: null).GetSection("Uploads");

    ConfigurationSourceInspector.Resolve(section, "MaxRequestMegabytes").ShouldBe((SettingSource.Environment, "100"));
    ConfigurationSourceInspector.Resolve(section, "Unknown").ShouldBe((SettingSource.Default, null));
  }

  [Fact]
  public void Resolve_WithInvalidArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => ConfigurationSourceInspector.Resolve(null!, Key));
    Should.Throw<ArgumentException>(() => ConfigurationSourceInspector.Resolve(new ConfigurationBuilder().Build(), " "));
  }

  private static IConfigurationRoot Build(string? store, string? stronger)
  {
    var builder = new ConfigurationBuilder().AddCodeCoverageSettingsStore(UnreachableDatabase, DatabaseProvider.PostgreSql);

    if (stronger is not null)
    {
      builder.AddInMemoryCollection(new Dictionary<string, string?> { [Key] = stronger });
    }

    var root = builder.Build();

    if (store is not null)
    {
      root.Providers.OfType<DatabaseSettingsConfigurationProvider>().Single().Set(Key, store);
    }

    return root;
  }

  #endregion
}
