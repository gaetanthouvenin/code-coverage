// ------------------------------------------------------------------------------------------------
// <copyright file="SampleCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

internal sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
{
  #region Ctors

  public SampleCommandValidator()
  {
    RuleFor(c => c.Name).NotEmpty().WithMessage("The name is required.");
  }

  #endregion
}
