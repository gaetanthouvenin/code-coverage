// ------------------------------------------------------------------------------------------------
// <copyright file="ReplaceSecretCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Settings.Commands.ReplaceSecret;

/// <summary>Validator for <see cref="ReplaceSecretCommand" />.</summary>
/// <remarks>
///   An unknown key or a setting that is not a secret is left to the handler, which refuses it with
///   its own error code.
/// </remarks>
internal sealed class ReplaceSecretCommandValidator : AbstractValidator<ReplaceSecretCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ReplaceSecretCommandValidator" /> class.</summary>
  public ReplaceSecretCommandValidator()
  {
    RuleFor(c => c.Key).NotEmpty().WithMessage("The key of the secret is required.");

    // The message never repeats the value: a secret never reaches a response or a log.
    RuleFor(c => c.Value).NotEmpty().WithMessage("The secret is required.");
  }

  #endregion
}
