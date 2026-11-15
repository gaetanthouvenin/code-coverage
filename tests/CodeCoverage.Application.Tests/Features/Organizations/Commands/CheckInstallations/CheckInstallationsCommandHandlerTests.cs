// ------------------------------------------------------------------------------------------------
// <copyright file="CheckInstallationsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Organizations.Commands.CheckInstallations;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Organizations.Commands.CheckInstallations;

public sealed class CheckInstallationsCommandHandlerTests
{
  #region Fields

  private readonly IAuditLog _audit = A.Fake<IAuditLog>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The request queues the coordinator of the instance lane and asks GitHub nothing itself.
  [Fact]
  public async Task Handle_ShouldQueueTheListingOfTheInstallationsAndAudit()
  {
    await new CheckInstallationsCommandHandler(_audit, _jobQueue).Handle(new CheckInstallationsCommand(), Ct);

    A.CallTo(
       () => _jobQueue.EnqueueAsync(
         A<JobRequest>.That.Matches(j => j.Type == JobTypes.SyncInstallations && j.IsInstance && j.OrganizationId == null),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _audit.RecordAsync("Organization.CheckInstallations", "GitHub", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Command_ShouldBeReservedToTheAdministratorsOfTheInstance()
  {
    typeof(IAdminOnly).IsAssignableFrom(typeof(CheckInstallationsCommand)).ShouldBeTrue();
  }

  #endregion
}
