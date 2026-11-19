// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectOrganizationCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Connections.Commands.ConnectOrganization;

/// <summary>Validator for <see cref="ConnectOrganizationCommand" />.</summary>
internal sealed class ConnectOrganizationCommandValidator : AbstractValidator<ConnectOrganizationCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ConnectOrganizationCommandValidator" /> class.</summary>
  /// <param name="availability">Tells the forges an organization can be admitted on, read at each validation.</param>
  public ConnectOrganizationCommandValidator(IForgeAvailability availability)
  {
    ArgumentNullException.ThrowIfNull(availability);

    RuleFor(static c => c.Forge)
      .Cascade(CascadeMode.Stop)
      .Must(static forge => forge is not Forge.GitHub)
      .WithErrorCode(ConnectionErrors.UseInstallation)
      .WithMessage("A GitHub account is admitted by the installation of the GitHub App, not by a connection.")
      .Must(forge => availability.AdmissionForges.Contains(forge))
      .WithErrorCode(ConnectionErrors.ForgeNotAvailable)
      .WithMessage("The forge is not offered for an admission on this instance.");

    RuleFor(static c => c.Login)
      .Must(Organization.IsWellFormedLogin)
      .WithErrorCode(OrganizationErrors.InvalidLogin)
      .WithMessage("The login is not one a forge can give.");
  }

  #endregion
}
