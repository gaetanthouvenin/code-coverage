// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveWebhookCommandValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.GitHubEvents;
using CodeCoverage.Application.Features.GitHubEvents.Commands.ReceiveWebhook;
using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.ReceiveWebhook;

public sealed class ReceiveWebhookCommandValidatorTests
{
  #region Methods

  [Fact]
  public void Validate_Complete_ShouldPass()
  {
    new ReceiveWebhookCommandValidator().Validate(new ReceiveWebhookCommand("d-1", "push", null, [])).IsValid.ShouldBeTrue();
  }

  [Theory]
  [InlineData("", "push")]
  [InlineData(" ", "push")]
  [InlineData("d-1", "")]
  public void Validate_Missing_ShouldRefuse(string deliveryId, string @event)
  {
    var error = new ReceiveWebhookCommandValidator().Validate(new ReceiveWebhookCommand(deliveryId, @event, null, [])).Errors.ShouldHaveSingleItem();

    error.ErrorCode.ShouldBe(GitHubEventErrors.InvalidDelivery);
  }

  [Fact]
  public void Validate_TooLong_ShouldRefuse()
  {
    var command = new ReceiveWebhookCommand(new string('d', GitHubWebhookEvent.DeliveryIdMaxLength + 1), new string('e', GitHubWebhookEvent.EventMaxLength + 1), null, []);

    new ReceiveWebhookCommandValidator().Validate(command).Errors.Count.ShouldBe(2);
  }

  #endregion
}
