// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsOptionsCatalogTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Tests.Settings;

// A setting of an organization read through IOptionsMonitor would have one value for the whole process,
// so the two routes must not overlap.
public sealed class SettingsOptionsCatalogTests
{
  #region Fields

  private static readonly Type[] OptionsTypes =
  [
    typeof(CoverageOptions),
    typeof(UploadOptions),
    typeof(ProcessingOptions),
    typeof(NotificationOptions),
    typeof(AccessOptions),
    typeof(BadgeOptions),
    typeof(RetentionOptions),
    typeof(GitHubOptions),
    typeof(UrlOptions),
  ];

  // Instance keys read straight from the configuration, or not read yet: no options class holds them.
  private static readonly string[] KeysWithoutOptions =
  [
    SettingDefinitions.GitHubAppId,
    SettingDefinitions.GitHubClientSecret,
  ];

  #endregion

  #region Methods

  [Fact]
  public void Catalog_EveryKey_ShouldBeCoveredByOptionsOrSnapshot()
  {
    var instanceKeys = KeysOf(SettingScope.Instance).Except(KeysWithoutOptions).ToList();
    var optionKeys = OptionsTypes.SelectMany(OptionKeysOf).ToList();
    var organizationNames = KeysOf(SettingScope.Organization).Select(NameOf).ToList();

    optionKeys.ShouldBe(instanceKeys, ignoreOrder: true);
    Properties(typeof(OrganizationSettingsSnapshot)).Select(static p => p.Name).ShouldBe(organizationNames, ignoreOrder: true);
  }

  private static IEnumerable<string> KeysOf(SettingScope scope)
  {
    return SettingDefinitions.All.Where(d => d.Scope == scope).Select(static d => d.Key);
  }

  private static string NameOf(string key)
  {
    return key[(key.IndexOf(':', StringComparison.Ordinal) + 1)..];
  }

  private static IEnumerable<string> OptionKeysOf(Type optionsType)
  {
    var section = (string)optionsType.GetField(nameof(CoverageOptions.SectionName))!.GetRawConstantValue()!;

    return Properties(optionsType).Select(p => $"{section}:{p.Name}");
  }

  private static PropertyInfo[] Properties(Type type)
  {
    return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
  }

  #endregion
}
