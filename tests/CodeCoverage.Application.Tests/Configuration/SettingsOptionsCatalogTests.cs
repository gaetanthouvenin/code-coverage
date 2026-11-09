// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsOptionsCatalogTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Reflection;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Configuration;

// No source feeds the defaults of the catalog into the configuration: the initial value of a property is
// the default that applies, so the options and the catalog must stay in step.
public sealed class SettingsOptionsCatalogTests
{
  #region Fields

  public static readonly TheoryData<Type> OptionsTypes =
  [
    typeof(CoverageOptions),
    typeof(UploadOptions),
    typeof(ProcessingOptions),
    typeof(NotificationOptions),
    typeof(AccessOptions),
    typeof(BadgeOptions),
    typeof(RetentionOptions),
    typeof(UrlOptions),
  ];

  #endregion

  #region Methods

  [Theory]
  [MemberData(nameof(OptionsTypes))]
  public void Properties_OfOptionsClass_ShouldMatchTheSettingsOfItsGroup(Type optionsType)
  {
    var group = SectionName(optionsType);
    var settings = Definitions(group).Select(static d => d.Key).ToList();
    var properties = Properties(optionsType).Select(p => $"{group}:{p.Name}").ToList();

    settings.ShouldNotBeEmpty($"'{group}' must be a group of the catalog");
    settings.Except(properties).ShouldBeEmpty($"every setting of '{group}' needs a property on {optionsType.Name}");
    properties.Except(settings).ShouldBeEmpty($"every property of {optionsType.Name} needs a setting in '{group}'");
  }

  [Theory]
  [MemberData(nameof(OptionsTypes))]
  public void Properties_OfOptionsClass_ShouldHaveTheTypeAndDefaultOfTheirSetting(Type optionsType)
  {
    var group = SectionName(optionsType);
    var options = Activator.CreateInstance(optionsType)!;

    foreach (var property in Properties(optionsType))
    {
      var definition = SettingDefinitions.Find($"{group}:{property.Name}");

      definition.ShouldNotBeNull($"{optionsType.Name}.{property.Name} needs a setting");
      property.PropertyType.ShouldBe(ClrType(definition.Type), definition.Key);
      Invariant(property.GetValue(options)).ShouldBe(definition.Default, definition.Key);
    }
  }

  private static Type ClrType(SettingType type)
  {
    return type switch
    {
      SettingType.Number => typeof(int),
      SettingType.Boolean => typeof(bool),
      var _ => typeof(string),
    };
  }

  private static List<SettingDefinition> Definitions(string group)
  {
    return SettingDefinitions.All.Where(d => d.Group == group && d.Scope is SettingScope.Instance).ToList();
  }

  private static string? Invariant(object? value)
  {
    return value switch
    {
      bool flag => flag ? "true" : "false",
      int number => number.ToString(CultureInfo.InvariantCulture),
      var other => (string?)other,
    };
  }

  private static PropertyInfo[] Properties(Type optionsType)
  {
    return optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
  }

  private static string SectionName(Type optionsType)
  {
    return (string)optionsType.GetField(nameof(CoverageOptions.SectionName))!.GetRawConstantValue()!;
  }

  #endregion
}
