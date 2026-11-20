// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeDeliveryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.Forges;

public sealed class ForgeDeliveryTests
{
  #region Methods

  [Fact]
  public void Ctor_ShouldKeepEveryValue()
  {
    byte[] payload = [1, 2];

    var delivery = new ForgeDelivery(Forge.AzureDevOps, "d-1", "git.push", "updated", "conn-1", "7", payload);

    delivery.Forge.ShouldBe(Forge.AzureDevOps);
    delivery.DeliveryId.ShouldBe("d-1");
    delivery.Event.ShouldBe("git.push");
    delivery.Action.ShouldBe("updated");
    delivery.ConnectionId.ShouldBe("conn-1");
    delivery.RepositoryExternalId.ShouldBe("7");
    delivery.Payload.ShouldBeSameAs(payload);
  }

  [Fact]
  public void Ctor_NullDeliveryId_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeDelivery(Forge.AzureDevOps, null!, "e", null, "conn-1", null, [])).ParamName.ShouldBe("deliveryId");
  }

  [Fact]
  public void Ctor_NullEvent_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeDelivery(Forge.AzureDevOps, "d-1", null!, null, "conn-1", null, [])).ParamName.ShouldBe("@event");
  }

  [Fact]
  public void Ctor_NullConnection_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeDelivery(Forge.AzureDevOps, "d-1", "e", null, null!, null, [])).ParamName.ShouldBe("connectionId");
  }

  [Fact]
  public void Ctor_NullPayload_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeDelivery(Forge.AzureDevOps, "d-1", "e", null, "conn-1", null, null!)).ParamName.ShouldBe("payload");
  }

  #endregion
}
