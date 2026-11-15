// ------------------------------------------------------------------------------------------------
// <copyright file="ValidationExceptionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation.Results;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Application.Tests.Exceptions;

public sealed class ValidationExceptionTests
{
  #region Methods

  [Fact]
  public void Ctor_WithFailuresOnSameProperty_ShouldGroupErrors()
  {
    ValidationFailure[] failures =
    [
      new("Name", "The name is required."),
      new("Name", "The name must be owner/name."),
      new("Branch", "The branch is too long."),
    ];

    var exception = new ValidationException(failures);

    exception.ErrorCode.ShouldBe(GeneralErrors.ValidationFailed);
    exception.ErrorCode.ShouldBe("General.ValidationFailed");
    exception.Errors.Count.ShouldBe(2);
    exception.Errors["Name"].ShouldBe(["The name is required.", "The name must be owner/name."]);
    exception.Errors["Branch"].ShouldBe(["The branch is too long."]);
    exception.ShouldBeAssignableTo<ApplicationException>();
  }

  [Fact]
  public void Ctor_WithFailures_ShouldKeepTheErrorCodeOfEachMessage()
  {
    ValidationFailure[] failures =
    [
      new("WebPublicUrl", "The address must be an absolute HTTP or HTTPS URL.") { ErrorCode = "Setting.InvalidValue" },
      new("WebPublicUrl", "Without code."),
      new("AdminUserIds", "Give at least one identifier.") { ErrorCode = "Setting.InvalidAdministrators" },
    ];

    var exception = new ValidationException(failures);

    exception.ErrorCodes.Count.ShouldBe(2);
    exception.ErrorCodes["WebPublicUrl"].ShouldBe(["Setting.InvalidValue", string.Empty]);
    exception.ErrorCodes["AdminUserIds"].ShouldBe(["Setting.InvalidAdministrators"]);
    exception.Errors["WebPublicUrl"].ShouldBe(["The address must be an absolute HTTP or HTTPS URL.", "Without code."]);
  }

  [Fact]
  public void Ctor_WithoutFailures_ShouldHaveNoError()
  {
    var exception = new ValidationException([]);

    exception.Errors.ShouldBeEmpty();
    exception.ErrorCodes.ShouldBeEmpty();
  }

  [Fact]
  public void NotFoundException_For_ShouldBuildFeatureNotFoundCode()
  {
    var exception = NotFoundException.For("Repository", "my-org/code-coverage");

    exception.ErrorCode.ShouldBe("Repository.NotFound");
    exception.Message.ShouldBe("Repository my-org/code-coverage does not exist.");
    exception.ShouldBeAssignableTo<ApplicationException>();
  }

  #endregion
}
