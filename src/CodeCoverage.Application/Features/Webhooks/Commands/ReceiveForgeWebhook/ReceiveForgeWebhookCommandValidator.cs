// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveForgeWebhookCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Contracts.Webhooks;

namespace CodeCoverage.Application.Features.Webhooks.Commands.ReceiveForgeWebhook;

/// <summary>Validator for <see cref="ReceiveForgeWebhookCommand" />.</summary>
/// <remarks>
///   The connection is not checked here: an identifier that cannot be one is refused by the handler like an unknown
///   connection, with <see cref="WebhookErrors.InvalidSignature" />, so that the route is not an oracle.
/// </remarks>
internal sealed class ReceiveForgeWebhookCommandValidator : AbstractValidator<ReceiveForgeWebhookCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ReceiveForgeWebhookCommandValidator" /> class.</summary>
  public ReceiveForgeWebhookCommandValidator()
  {
    RuleFor(static c => c.DeliveryId)
      .Must(static id => !string.IsNullOrWhiteSpace(id) && id.Length <= ForgeWebhookRequest.DeliveryIdMaxLength)
      .WithErrorCode(WebhookErrors.InvalidDelivery)
      .WithMessage($"The delivery identifier is required and holds at most {ForgeWebhookRequest.DeliveryIdMaxLength} characters.");

    RuleFor(static c => c.Event)
      .Must(static name => !string.IsNullOrWhiteSpace(name) && name.Length <= ForgeWebhookRequest.EventMaxLength)
      .WithErrorCode(WebhookErrors.InvalidDelivery)
      .WithMessage($"The event is required and holds at most {ForgeWebhookRequest.EventMaxLength} characters.");

    RuleFor(static c => c.Headers)
      .NotNull()
      .WithErrorCode(WebhookErrors.InvalidDelivery)
      .WithMessage("The headers of the delivery are required.");

    RuleFor(static c => c.Payload)
      .NotNull()
      .WithErrorCode(WebhookErrors.InvalidPayload)
      .WithMessage("The body of the delivery is required.");
  }

  #endregion
}
