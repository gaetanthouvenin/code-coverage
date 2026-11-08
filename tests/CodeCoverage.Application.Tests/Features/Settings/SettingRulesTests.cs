// ------------------------------------------------------------------------------------------------
// <copyright file="SettingRulesTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Domain.Settings;

using static CodeCoverage.Application.Tests.Features.Settings.Support.SettingsStoreFakes;

namespace CodeCoverage.Application.Tests.Features.Settings;

public sealed class SettingRulesTests
{
  #region Methods

  [Theory]
  [InlineData(0)]
  [InlineData(SettingDefinitions.CoverageOrganizationYamlMaxLength)]
  public void Check_YamlWithinTheLimit_ShouldAccept(int length)
  {
    var definition = Definition(SettingDefinitions.CoverageOrganizationYaml);

    SettingRules.Check(definition, new string('a', length)).ShouldBeNull();
  }

  [Fact]
  public void Check_YamlOverTheLimit_ShouldReturnOutOfRange()
  {
    var definition = Definition(SettingDefinitions.CoverageOrganizationYaml);
    var value = new string('a', SettingDefinitions.CoverageOrganizationYamlMaxLength + 1);

    SettingRules.Check(definition, value).ShouldBe(SettingErrors.OutOfRange);
  }

  [Fact]
  public void Check_TextWithoutLimit_ShouldAcceptAnyLength()
  {
    var definition = Definition(SettingDefinitions.NotificationsCommentTitle);

    SettingRules.Check(definition, new string('a', 100_000)).ShouldBeNull();
  }

  #endregion
}
