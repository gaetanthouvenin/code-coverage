// ------------------------------------------------------------------------------------------------
// <copyright file="CreateOrganizationUploadTokenCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;

/// <summary>Validator for <see cref="CreateOrganizationUploadTokenCommand" />.</summary>
internal sealed class CreateOrganizationUploadTokenCommandValidator : AbstractValidator<CreateOrganizationUploadTokenCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CreateOrganizationUploadTokenCommandValidator" /> class.</summary>
  /// <param name="clock">The clock.</param>
  public CreateOrganizationUploadTokenCommandValidator(IClock clock)
  {
    RuleFor(static c => c.Label).MustBeALabel();
    RuleFor(static c => c.ImportedValue).MustBeACodecovToken();
    RuleFor(static c => c.ExpiresAt).MustBeInTheFuture(clock);
  }

  #endregion
}
