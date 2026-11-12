// ------------------------------------------------------------------------------------------------
// <copyright file="CreateApiTokenCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access.Commands.CreateApiToken;

/// <summary>Validator for <see cref="CreateApiTokenCommand" />.</summary>
internal sealed class CreateApiTokenCommandValidator : AbstractValidator<CreateApiTokenCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CreateApiTokenCommandValidator" /> class.</summary>
  /// <param name="clock">The clock.</param>
  public CreateApiTokenCommandValidator(IClock clock)
  {
    RuleFor(static c => c.Name)
      .Must(static name => !string.IsNullOrWhiteSpace(name) && name.Length <= ApiToken.NameMaxLength)
      .WithErrorCode(AccessErrors.InvalidApiTokenName)
      .WithMessage($"Give the token a name of 1 to {ApiToken.NameMaxLength} characters.");

    RuleFor(static c => c.Organization)
      .Must(static (command, organization) => command.Forge is null == string.IsNullOrWhiteSpace(organization))
      .WithErrorCode(AccessErrors.InvalidApiTokenOrganization)
      .WithMessage("Name both the forge and the login of the organization, or neither for a token of the instance.");

    RuleFor(static c => c.ExpiresAt)
      .Must(expiresAt => expiresAt is null || expiresAt > clock.UtcNow)
      .WithErrorCode(AccessErrors.InvalidApiTokenExpiry)
      .WithMessage("The expiry must be in the future.");
  }

  #endregion
}
