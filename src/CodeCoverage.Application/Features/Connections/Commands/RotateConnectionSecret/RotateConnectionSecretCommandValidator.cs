// ------------------------------------------------------------------------------------------------
// <copyright file="RotateConnectionSecretCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Connections;

namespace CodeCoverage.Application.Features.Connections.Commands.RotateConnectionSecret;

/// <summary>Validator for <see cref="RotateConnectionSecretCommand" />.</summary>
internal sealed class RotateConnectionSecretCommandValidator : AbstractValidator<RotateConnectionSecretCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="RotateConnectionSecretCommandValidator" /> class.</summary>
  public RotateConnectionSecretCommandValidator()
  {
    RuleFor(static c => c.ConnectionId)
      .Must(ForgeConnection.IsWellFormedId)
      .WithErrorCode(ConnectionErrors.NotFound)
      .WithMessage("The identifier is not one of a connection.");

    RuleFor(static c => c.Kind).IsInEnum().WithErrorCode(GeneralErrors.ValidationFailed);

    RuleFor(static c => c.Credential)
      .Must(static credential => !string.IsNullOrWhiteSpace(credential))
      .When(static c => c.Kind is ConnectionSecretKind.Credential)
      .WithErrorCode(ConnectionErrors.CredentialRefused)
      .WithMessage("A new credential is required.");

    RuleFor(static c => c.Credential)
      .Null()
      .When(static c => c.Kind is ConnectionSecretKind.WebhookSecret)
      .WithErrorCode(GeneralErrors.ValidationFailed)
      .WithMessage("The instance draws the secret of the webhooks: none is given.");
  }

  #endregion
}
