// ------------------------------------------------------------------------------------------------
// <copyright file="CreateRepositoryUploadTokenCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.CreateRepositoryUploadToken;

/// <summary>Validator for <see cref="CreateRepositoryUploadTokenCommand" />.</summary>
internal sealed class CreateRepositoryUploadTokenCommandValidator : AbstractValidator<CreateRepositoryUploadTokenCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CreateRepositoryUploadTokenCommandValidator" /> class.</summary>
  /// <param name="clock">The clock.</param>
  public CreateRepositoryUploadTokenCommandValidator(IClock clock)
  {
    RuleFor(static c => c.Label).MustBeALabel();
    RuleFor(static c => c.ImportedValue).MustBeACodecovToken();
    RuleFor(static c => c.ExpiresAt).MustBeInTheFuture(clock);
  }

  #endregion
}
