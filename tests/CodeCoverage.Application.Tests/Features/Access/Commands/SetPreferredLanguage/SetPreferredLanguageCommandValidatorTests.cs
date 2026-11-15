// ------------------------------------------------------------------------------------------------
// <copyright file="SetPreferredLanguageCommandValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Access.Commands.SetPreferredLanguage;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.SetPreferredLanguage;

public sealed class SetPreferredLanguageCommandValidatorTests
{
  #region Fields

  private readonly SetPreferredLanguageCommandValidator _validator = new();

  #endregion

  #region Methods

  [Theory]
  [InlineData("en")]
  [InlineData("it")]
  [InlineData("es")]
  public void Validate_WithTwoLowerCaseLetters_ShouldPass(string language)
  {
    _validator.Validate(new SetPreferredLanguageCommand(language)).IsValid.ShouldBeTrue();
  }

  [Theory]
  [InlineData("")]
  [InlineData("IT")]
  [InlineData("ita")]
  [InlineData("es-MX")]
  [InlineData(null)]
  public void Validate_WithAnythingElse_ShouldFailWithInvalidPreferredLanguage(string? language)
  {
    var result = _validator.Validate(new SetPreferredLanguageCommand(language!));

    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(AccessErrors.InvalidPreferredLanguage);
  }

  #endregion
}
