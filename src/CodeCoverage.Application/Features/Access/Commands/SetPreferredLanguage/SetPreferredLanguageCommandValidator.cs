// ------------------------------------------------------------------------------------------------
// <copyright file="SetPreferredLanguageCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Commands.SetPreferredLanguage;

/// <summary>Validator for <see cref="SetPreferredLanguageCommand" />.</summary>
internal sealed class SetPreferredLanguageCommandValidator : AbstractValidator<SetPreferredLanguageCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SetPreferredLanguageCommandValidator" /> class.</summary>
  public SetPreferredLanguageCommandValidator()
  {
    RuleFor(static c => c.Language)
      .Must(AppUser.IsLanguageCode)
      .WithErrorCode(AccessErrors.InvalidPreferredLanguage)
      .WithMessage("The language is a code of two lower-case letters, such as « it ».");
  }

  #endregion
}
