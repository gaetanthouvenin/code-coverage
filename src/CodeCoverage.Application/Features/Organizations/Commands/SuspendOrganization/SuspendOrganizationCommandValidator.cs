// ------------------------------------------------------------------------------------------------
// <copyright file="SuspendOrganizationCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;

/// <summary>Validator for <see cref="SuspendOrganizationCommand" />.</summary>
internal sealed class SuspendOrganizationCommandValidator : AbstractValidator<SuspendOrganizationCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SuspendOrganizationCommandValidator" /> class.</summary>
  public SuspendOrganizationCommandValidator()
  {
    RuleFor(static c => c.Login)
      .Must(Organization.IsWellFormedLogin)
      .WithErrorCode(OrganizationErrors.InvalidLogin)
      .WithMessage("The login is not one a forge can give.");
  }

  #endregion
}
