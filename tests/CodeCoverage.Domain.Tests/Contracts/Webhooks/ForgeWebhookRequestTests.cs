// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeWebhookRequestTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.Webhooks;

public sealed class ForgeWebhookRequestTests
{
  #region Fields

  private static readonly Dictionary<string, string> Headers = new(StringComparer.Ordinal) { ["Authorization"] = "Basic abc" };

  #endregion

  #region Methods

  [Fact]
  public void Ctor_ShouldKeepEveryValue()
  {
    byte[] payload = [1, 2];

    var request = new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", "d-1", "git.push", Headers, payload);

    request.Forge.ShouldBe(Forge.AzureDevOps);
    request.ConnectionId.ShouldBe("conn-1");
    request.DeliveryId.ShouldBe("d-1");
    request.Event.ShouldBe("git.push");
    request.Headers["Authorization"].ShouldBe("Basic abc");
    request.Payload.ShouldBeSameAs(payload);
  }

  [Fact]
  public void Headers_OtherCase_ShouldFindTheHeader()
  {
    var request = new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", "d-1", "git.push", Headers, []);

    request.Headers["authorization"].ShouldBe("Basic abc");
  }

  [Fact]
  public void Headers_SourceChangedAfterwards_ShouldKeepTheReceivedOnes()
  {
    var source = new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Signature"] = "first" };
    var request = new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", "d-1", "git.push", source, []);

    source["X-Signature"] = "second";

    request.Headers["X-Signature"].ShouldBe("first");
  }

  [Fact]
  public void Ctor_NullConnection_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeWebhookRequest(Forge.AzureDevOps, null!, "d-1", "e", Headers, [])).ParamName.ShouldBe("connectionId");
  }

  [Fact]
  public void Ctor_NullDeliveryId_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", null!, "e", Headers, [])).ParamName.ShouldBe("deliveryId");
  }

  [Fact]
  public void Ctor_NullEvent_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", "d-1", null!, Headers, [])).ParamName.ShouldBe("@event");
  }

  [Fact]
  public void Ctor_NullHeaders_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", "d-1", "e", null!, [])).ParamName.ShouldBe("headers");
  }

  [Fact]
  public void Ctor_NullPayload_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", "d-1", "e", Headers, null!)).ParamName.ShouldBe("payload");
  }

  #endregion
}
