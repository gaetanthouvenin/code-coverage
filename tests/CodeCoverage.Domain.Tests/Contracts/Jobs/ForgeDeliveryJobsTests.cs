// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeDeliveryJobsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class ForgeDeliveryJobsTests
{
  #region Methods

  [Fact]
  public void Payload_ShouldNameTheForgeAndTheDelivery()
  {
    ForgeDeliveryJobs.Payload(Forge.AzureDevOps, "d-1").ShouldBe("""{"Forge":"AzureDevOps","DeliveryId":"d-1"}""");
  }

  [Fact]
  public void Payload_GitHub_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => ForgeDeliveryJobs.Payload(Forge.GitHub, "d-1")).ParamName.ShouldBe("forge");
  }

  [Fact]
  public void Payload_UnknownForge_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => ForgeDeliveryJobs.Payload((Forge)42, "d-1"));
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Payload_EmptyDelivery_ShouldThrowArgumentException(string deliveryId)
  {
    Should.Throw<ArgumentException>(() => ForgeDeliveryJobs.Payload(Forge.AzureDevOps, deliveryId));
  }

  #endregion
}
