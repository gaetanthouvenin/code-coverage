// ------------------------------------------------------------------------------------------------
// <copyright file="CreateOrganizationUploadTokenCommandValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.UploadTokens;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

using static CodeCoverage.Application.Tests.Features.UploadTokens.UploadTokenFixture;

namespace CodeCoverage.Application.Tests.Features.UploadTokens.Commands.CreateOrganizationUploadToken;

public sealed class CreateOrganizationUploadTokenCommandValidatorTests
{
  #region Fields

  private readonly CreateOrganizationUploadTokenCommandValidator _validator;

  #endregion

  #region Ctors

  public CreateOrganizationUploadTokenCommandValidatorTests()
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);
    _validator = new CreateOrganizationUploadTokenCommandValidator(clock);
  }

  #endregion

  #region Methods

  [Theory]
  [InlineData(null)]
  [InlineData(Imported)]
  public void Validate_ValidCommand_ShouldPass(string? importedValue)
  {
    _validator.Validate(Command("ci", importedValue, Now.AddDays(1))).IsValid.ShouldBeTrue();
  }

  [Theory]
  [InlineData("not-a-uuid")]
  [InlineData("cvc_ab12")]
  public void Validate_ImportedValueNotAUuid_ShouldReportItsCode(string importedValue)
  {
    ErrorCode(Command("ci", importedValue, null)).ShouldBe(UploadTokenErrors.InvalidImportedValue);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Validate_EmptyLabel_ShouldReportItsCode(string label)
  {
    ErrorCode(Command(label, null, null)).ShouldBe(UploadTokenErrors.InvalidLabel);
  }

  [Fact]
  public void Validate_LabelTooLong_ShouldReportItsCode()
  {
    ErrorCode(Command(new string('l', UploadToken.LabelMaxLength + 1), null, null)).ShouldBe(UploadTokenErrors.InvalidLabel);
  }

  [Fact]
  public void Validate_ExpiryInThePast_ShouldReportItsCode()
  {
    ErrorCode(Command("ci", null, Now.AddSeconds(-1))).ShouldBe(UploadTokenErrors.InvalidExpiry);
  }

  private static CreateOrganizationUploadTokenCommand Command(string label, string? importedValue, DateTimeOffset? expiresAt) =>
    new(Forge.GitHub, "acme", label, importedValue, expiresAt);

  private string ErrorCode(CreateOrganizationUploadTokenCommand command) => _validator.Validate(command).Errors.ShouldHaveSingleItem().ErrorCode;

  #endregion
}
