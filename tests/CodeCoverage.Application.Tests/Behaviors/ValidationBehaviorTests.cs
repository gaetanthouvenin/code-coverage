// ------------------------------------------------------------------------------------------------
// <copyright file="ValidationBehaviorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Application.Behaviors;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Tests.Behaviors.Support;

using ValidationException = CodeCoverage.Application.Exceptions.ValidationException;

namespace CodeCoverage.Application.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
  #region Fields

  private int _nextCalls;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_WithFailingRule_ShouldThrowValidationException()
  {
    var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);

    var exception = await Should.ThrowAsync<ValidationException>(
                      async () => await behavior.Handle(
                                    new SampleCommand(string.Empty, "token"),
                                    Next,
                                    TestContext.Current.CancellationToken));

    exception.ErrorCode.ShouldBe(GeneralErrors.ValidationFailed);
    exception.Errors.Keys.ShouldBe(["Name"]);
    exception.Errors["Name"].ShouldBe(["The name is required."]);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_WithSeveralValidators_ShouldGatherEveryFailure()
  {
    var behavior = new ValidationBehavior<SampleCommand, string>(
                     [new SampleCommandValidator(), new SampleCommandValidator()]);

    var exception = await Should.ThrowAsync<ValidationException>(
                      async () => await behavior.Handle(
                                    new SampleCommand(string.Empty, "token"),
                                    Next,
                                    TestContext.Current.CancellationToken));

    exception.Errors["Name"].Length.ShouldBe(2);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_WithPassingRules_ShouldCallNext()
  {
    var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);

    var response = await behavior.Handle(new SampleCommand("main", "token"), Next, TestContext.Current.CancellationToken);

    response.ShouldBe("handled");
    _nextCalls.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_WithoutValidator_ShouldCallNext()
  {
    var behavior = new ValidationBehavior<SampleCommand, string>([]);

    var response = await behavior.Handle(new SampleCommand(string.Empty, "token"), Next, TestContext.Current.CancellationToken);

    response.ShouldBe("handled");
    _nextCalls.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_WithNullNext_ShouldThrowArgumentNullException()
  {
    var behavior = new ValidationBehavior<SampleCommand, string>(Array.Empty<IValidator<SampleCommand>>());

    await Should.ThrowAsync<ArgumentNullException>(
      async () => await behavior.Handle(new SampleCommand("main", "token"), null!, TestContext.Current.CancellationToken));
  }

  private ValueTask<string> Next(SampleCommand message, CancellationToken cancellationToken)
  {
    _nextCalls++;

    return ValueTask.FromResult("handled");
  }

  #endregion
}
