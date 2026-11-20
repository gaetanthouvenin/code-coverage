// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveForgeWebhookCommandValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Webhooks;
using CodeCoverage.Application.Features.Webhooks.Commands.ReceiveForgeWebhook;
using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Tests.Features.Webhooks.Commands.ReceiveForgeWebhook;

public sealed class ReceiveForgeWebhookCommandValidatorTests
{
  #region Methods

  [Fact]
  public void Validate_Complete_ShouldPass()
  {
    new ReceiveForgeWebhookCommandValidator().Validate(Command()).IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_MalformedConnection_ShouldPass()
  {
    // The handler refuses it like an unknown connection, so that the route is not an oracle.
    new ReceiveForgeWebhookCommandValidator().Validate(Command() with { ConnectionId = "Not/A Connection" }).IsValid.ShouldBeTrue();
  }

  [Theory]
  [InlineData("", "git.push")]
  [InlineData(" ", "git.push")]
  [InlineData(null, "git.push")]
  [InlineData("d-1", "")]
  [InlineData("d-1", null)]
  public void Validate_Missing_ShouldRefuse(string? deliveryId, string? @event)
  {
    var error = new ReceiveForgeWebhookCommandValidator().Validate(Command() with { DeliveryId = deliveryId!, Event = @event! })
                                                         .Errors
                                                         .ShouldHaveSingleItem();

    error.ErrorCode.ShouldBe(WebhookErrors.InvalidDelivery);
  }

  [Fact]
  public void Validate_TooLong_ShouldRefuse()
  {
    var command = Command() with
    {
      DeliveryId = new string('d', ForgeWebhookRequest.DeliveryIdMaxLength + 1),
      Event = new string('e', ForgeWebhookRequest.EventMaxLength + 1),
    };

    new ReceiveForgeWebhookCommandValidator().Validate(command).Errors.Count.ShouldBe(2);
  }

  [Fact]
  public void Validate_LongestTexts_ShouldPass()
  {
    var command = Command() with
    {
      DeliveryId = new string('d', ForgeWebhookRequest.DeliveryIdMaxLength),
      Event = new string('e', ForgeWebhookRequest.EventMaxLength),
    };

    new ReceiveForgeWebhookCommandValidator().Validate(command).IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_NullHeaders_ShouldRefuse()
  {
    var error = new ReceiveForgeWebhookCommandValidator().Validate(Command() with { Headers = null! }).Errors.ShouldHaveSingleItem();

    error.ErrorCode.ShouldBe(WebhookErrors.InvalidDelivery);
  }

  [Fact]
  public void Validate_NullPayload_ShouldRefuse()
  {
    var error = new ReceiveForgeWebhookCommandValidator().Validate(Command() with { Payload = null! }).Errors.ShouldHaveSingleItem();

    error.ErrorCode.ShouldBe(WebhookErrors.InvalidPayload);
  }

  private static ReceiveForgeWebhookCommand Command() =>
    new(Forge.AzureDevOps, "contoso-devops", "d-1", "git.push", new Dictionary<string, string>(StringComparer.Ordinal), "{}"u8.ToArray());

  #endregion
}
