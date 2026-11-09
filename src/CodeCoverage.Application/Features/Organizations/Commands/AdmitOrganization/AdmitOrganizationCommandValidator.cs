// ------------------------------------------------------------------------------------------------
// <copyright file="AdmitOrganizationCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;

/// <summary>Validator for <see cref="AdmitOrganizationCommand" />.</summary>
internal sealed class AdmitOrganizationCommandValidator : AbstractValidator<AdmitOrganizationCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="AdmitOrganizationCommandValidator" /> class.</summary>
  public AdmitOrganizationCommandValidator()
  {
    RuleFor(static c => c.Login)
      .Must(Organization.IsWellFormedLogin)
      .WithErrorCode(OrganizationErrors.InvalidLogin)
      .WithMessage("The login is not one a forge can give.");

    RuleFor(static c => c.ExternalAccountId)
      .Must(static id => !string.IsNullOrWhiteSpace(id) && id.Length <= Organization.ExternalAccountIdMaxLength)
      .WithErrorCode(OrganizationErrors.InvalidAccountId)
      .WithMessage($"The account identifier is required and holds at most {Organization.ExternalAccountIdMaxLength} characters.");
  }

  #endregion
}
