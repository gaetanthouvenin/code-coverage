// ------------------------------------------------------------------------------------------------
// <copyright file="SettingDefinitionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Tests.Settings;

public sealed class SettingDefinitionTests
{
  #region Methods

  [Fact]
  public void Create_WithValidValues_ShouldKeepThem()
  {
    var definition = SettingDefinition.Create(
      "Processing:WorkerConcurrency",
      "Processing",
      SettingType.Number,
      "4",
      requiresRestart: true,
      min: 1,
      max: 16
    );

    definition.Key.ShouldBe("Processing:WorkerConcurrency");
    definition.Group.ShouldBe("Processing");
    definition.Type.ShouldBe(SettingType.Number);
    definition.Default.ShouldBe("4");
    definition.RequiresRestart.ShouldBeTrue();
    definition.Min.ShouldBe(1);
    definition.Max.ShouldBe(16);
    definition.IsSecret.ShouldBeFalse();
    definition.ToString().ShouldBe("Processing:WorkerConcurrency");
  }

  [Fact]
  public void Create_WithDefaults_ShouldNeitherRestartNorBeSecret()
  {
    var definition = SettingDefinition.Create("Badges:Label", "Badges", SettingType.Text);

    definition.Default.ShouldBeNull();
    definition.RequiresRestart.ShouldBeFalse();
    definition.Min.ShouldBeNull();
    definition.Max.ShouldBeNull();
    definition.IsSecret.ShouldBeFalse();
  }

  [Theory]
  [InlineData("NoSection")]
  [InlineData(":Name")]
  [InlineData("Section:")]
  public void Create_WithKeyWithoutSection_ShouldThrowArgumentException(string key)
  {
    Should.Throw<ArgumentException>(() => SettingDefinition.Create(key, "Group", SettingType.Text))
          .ParamName.ShouldBe("key");
  }

  [Theory]
  [InlineData(null, "Group", "key")]
  [InlineData("Section:Name", null, "group")]
  public void Create_WithNullText_ShouldThrowArgumentNullException(
    string? key,
    string? group,
    string parameter)
  {
    Should.Throw<ArgumentNullException>(() => SettingDefinition.Create(key!, group!, SettingType.Text))
          .ParamName.ShouldBe(parameter);
  }

  [Theory]
  [InlineData(" ", "Group", "key")]
  [InlineData("Section:Name", "", "group")]
  public void Create_WithBlankText_ShouldThrowArgumentException(string key, string group, string parameter)
  {
    Should.Throw<ArgumentException>(() => SettingDefinition.Create(key, group, SettingType.Text))
          .ParamName.ShouldBe(parameter);
  }

  [Fact]
  public void Create_WithUndefinedType_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => SettingDefinition.Create("A:B", "A", (SettingType)42))
          .ParamName.ShouldBe("type");
  }

  [Fact]
  public void Create_WithBoundsOnText_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => SettingDefinition.Create("A:B", "A", SettingType.Text, min: 1))
          .ParamName.ShouldBe("type");
  }

  [Fact]
  public void Create_WithReversedBounds_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(
            () => SettingDefinition.Create("A:B", "A", SettingType.Number, min: 10, max: 1)
          )
          .ParamName.ShouldBe("min");
  }

  [Fact]
  public void Create_SecretWithDefault_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(
            () => SettingDefinition.Create("A:B", "A", SettingType.Text, "value", isSecret: true)
          )
          .ParamName.ShouldBe("defaultValue");
  }

  [Fact]
  public void Create_SecretNotText_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => SettingDefinition.Create("A:B", "A", SettingType.Url, isSecret: true))
          .ParamName.ShouldBe("type");
  }

  #endregion
}
