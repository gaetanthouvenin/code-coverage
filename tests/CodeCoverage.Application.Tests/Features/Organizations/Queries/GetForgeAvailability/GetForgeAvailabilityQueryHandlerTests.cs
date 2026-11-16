// ------------------------------------------------------------------------------------------------
// <copyright file="GetForgeAvailabilityQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Organizations.Queries.GetForgeAvailability;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Organizations.Queries.GetForgeAvailability;

public sealed class GetForgeAvailabilityQueryHandlerTests
{
  #region Fields

  private readonly IForgeAvailability _availability = A.Fake<IForgeAvailability>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Two contributors: GitHub admits and signs in, Azure DevOps admits before its Web sign-in exists.
  [Fact]
  public async Task Handle_TwoContributors_ShouldListBoth()
  {
    A.CallTo(() => _availability.AdmissionForges).Returns([Forge.GitHub, Forge.AzureDevOps]);
    A.CallTo(() => _availability.SignInForges).Returns([Forge.GitHub]);

    var snapshot = await new GetForgeAvailabilityQueryHandler(_availability).Handle(new GetForgeAvailabilityQuery(), Ct);

    snapshot.AdmissionForges.ShouldBe([Forge.GitHub, Forge.AzureDevOps]);
    snapshot.SignInForges.ShouldBe([Forge.GitHub]);
  }

  [Fact]
  public async Task Handle_NoForgeAvailable_ShouldReturnEmptyLists()
  {
    A.CallTo(() => _availability.AdmissionForges).Returns([]);
    A.CallTo(() => _availability.SignInForges).Returns([]);

    var snapshot = await new GetForgeAvailabilityQueryHandler(_availability).Handle(new GetForgeAvailabilityQuery(), Ct);

    snapshot.AdmissionForges.ShouldBeEmpty();
    snapshot.SignInForges.ShouldBeEmpty();
  }

  #endregion
}
