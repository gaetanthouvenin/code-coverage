// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveWebhookCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.ReceiveWebhook;

/// <summary>Validator for <see cref="ReceiveWebhookCommand" />.</summary>
internal sealed class ReceiveWebhookCommandValidator : AbstractValidator<ReceiveWebhookCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ReceiveWebhookCommandValidator" /> class.</summary>
  public ReceiveWebhookCommandValidator()
  {
    RuleFor(static c => c.DeliveryId)
      .Must(static id => !string.IsNullOrWhiteSpace(id) && id.Length <= GitHubWebhookEvent.DeliveryIdMaxLength)
      .WithErrorCode(GitHubEventErrors.InvalidDelivery)
      .WithMessage($"The delivery identifier is required and holds at most {GitHubWebhookEvent.DeliveryIdMaxLength} characters.");

    RuleFor(static c => c.Event)
      .Must(static name => !string.IsNullOrWhiteSpace(name) && name.Length <= GitHubWebhookEvent.EventMaxLength)
      .WithErrorCode(GitHubEventErrors.InvalidDelivery)
      .WithMessage($"The event is required and holds at most {GitHubWebhookEvent.EventMaxLength} characters.");
  }

  #endregion
}
