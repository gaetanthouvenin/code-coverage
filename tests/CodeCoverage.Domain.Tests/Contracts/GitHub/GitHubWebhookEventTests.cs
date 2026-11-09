// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubWebhookEventTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Domain.Tests.Contracts.GitHub;

public sealed class GitHubWebhookEventTests
{
  #region Methods

  [Fact]
  public void TryParse_ShouldReadTheFieldsThatRouteTheDelivery()
  {
    var body = Bytes("""{"action":"opened","installation":{"id":42},"repository":{"id":7}}""");

    var delivery = GitHubWebhookEvent.TryParse("d-1", "pull_request", body).ShouldNotBeNull();

    delivery.DeliveryId.ShouldBe("d-1");
    delivery.Event.ShouldBe("pull_request");
    delivery.Action.ShouldBe("opened");
    delivery.InstallationId.ShouldBe(42);
    delivery.RepositoryId.ShouldBe(7);
    delivery.Payload.ShouldBeSameAs(body);
  }

  [Fact]
  public void TryParse_WithoutOptionalFields_ShouldKeepThemEmpty()
  {
    var delivery = GitHubWebhookEvent.TryParse("d-1", "ping", Bytes("{}")).ShouldNotBeNull();

    delivery.Action.ShouldBeNull();
    delivery.InstallationId.ShouldBeNull();
    delivery.RepositoryId.ShouldBeNull();
  }

  [Theory]
  [InlineData("""{"installation":"42"}""")]
  [InlineData("""{"installation":{"id":"42"}}""")]
  [InlineData("""{"installation":{"id":0}}""")]
  [InlineData("""{"installation":{"id":-1}}""")]
  [InlineData("""{"installation":{"id":1.5}}""")]
  public void TryParse_UnreadableInstallation_ShouldKeepNone(string json)
  {
    GitHubWebhookEvent.TryParse("d-1", "push", Bytes(json)).ShouldNotBeNull().InstallationId.ShouldBeNull();
  }

  [Theory]
  [InlineData("")]
  [InlineData("not json")]
  [InlineData("[1,2]")]
  [InlineData("42")]
  public void TryParse_NotAnObject_ShouldReturnNull(string json)
  {
    GitHubWebhookEvent.TryParse("d-1", "push", Bytes(json)).ShouldBeNull();
  }

  [Fact]
  public void TryParse_ActionTooLong_ShouldReturnNull()
  {
    var body = Bytes($$"""{"action":"{{new string('a', GitHubWebhookEvent.ActionMaxLength + 1)}}"}""");

    GitHubWebhookEvent.TryParse("d-1", "push", body).ShouldBeNull();
  }

  private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

  #endregion
}
