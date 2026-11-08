// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateSettingCommandValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.UpdateSetting;

public sealed class UpdateSettingCommandValidatorTests
{
  #region Fields

  private readonly UpdateSettingCommandValidator _validator = new();

  #endregion

  #region Methods

  [Theory]
  [InlineData(SettingDefinitions.ProcessingWorkerConcurrency, "0")]
  [InlineData(SettingDefinitions.ProcessingWorkerConcurrency, "17")]
  [InlineData(SettingDefinitions.UploadsMaxRequestMegabytes, "1001")]
  public void Validate_WithNumberOutOfRange_ShouldFailWithOutOfRange(string key, string value)
  {
    var result = _validator.Validate(new UpdateSettingCommand(key, value));

    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(SettingErrors.OutOfRange);
  }

  [Theory]
  [InlineData(SettingDefinitions.ProcessingWorkerConcurrency, "four")]
  [InlineData(SettingDefinitions.ProcessingWorkerConcurrency, "4.5")]
  [InlineData(SettingDefinitions.CoverageAllowFilenameOnlyMatch, "yes")]
  [InlineData(SettingDefinitions.GitHubApiBaseUrl, "api.github.com")]
  [InlineData(SettingDefinitions.GitHubApiBaseUrl, "ftp://api.github.com")]
  public void Validate_WithValueOfAnotherType_ShouldFailWithInvalidValue(string key, string value)
  {
    var result = _validator.Validate(new UpdateSettingCommand(key, value));

    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(SettingErrors.InvalidValue);
  }

  [Theory]
  [InlineData(SettingDefinitions.ProcessingWorkerConcurrency, "1")]
  [InlineData(SettingDefinitions.ProcessingWorkerConcurrency, "16")]
  [InlineData(SettingDefinitions.UploadsMaxFilesPerUpload, "100000")]
  [InlineData(SettingDefinitions.CoverageAllowFilenameOnlyMatch, "false")]
  [InlineData(SettingDefinitions.UrlsWebPublicUrl, "https://coverage.example.com")]
  [InlineData(SettingDefinitions.AccessAdminUserIds, "1234, 5678")]
  public void Validate_WithValueOfTheType_ShouldPass(string key, string value)
  {
    _validator.Validate(new UpdateSettingCommand(key, value)).IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_OrganizationKey_ShouldLeaveItToTheHandler()
  {
    _validator.Validate(new UpdateSettingCommand(SettingDefinitions.NotificationsStatusesEnabled, "yes"))
              .IsValid
              .ShouldBeTrue();
  }

  [Fact]
  public void Validate_WithUnknownKey_ShouldLeaveTheRefusalToTheHandler()
  {
    _validator.Validate(new UpdateSettingCommand("Nope:Missing", "anything")).IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_WithoutKeyOrValue_ShouldFail()
  {
    var result = _validator.Validate(new UpdateSettingCommand(string.Empty, null!));

    result.Errors.Select(e => e.PropertyName)
          .ShouldBe([nameof(UpdateSettingCommand.Key), nameof(UpdateSettingCommand.Value)], ignoreOrder: true);
  }

  #endregion
}
