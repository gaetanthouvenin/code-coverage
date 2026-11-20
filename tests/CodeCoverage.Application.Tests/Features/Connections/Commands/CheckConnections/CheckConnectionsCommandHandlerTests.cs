// ------------------------------------------------------------------------------------------------
// <copyright file="CheckConnectionsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Connections.Commands.CheckConnections;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.CheckConnections;

public sealed class CheckConnectionsCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly List<ForgeConnection> _listed = [];

  private readonly List<JobRequest> _queued = [];

  private readonly CheckConnectionsCommandHandler _handler;

  #endregion

  #region Ctors

  public CheckConnectionsCommandHandlerTests()
  {
    A.CallTo(() => _connections.ListActiveAsync(A<CancellationToken>._))
     .ReturnsLazily(() => (IReadOnlyList<ForgeConnection>)[.. _listed]);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);

    _handler = new CheckConnectionsCommandHandler(_connections, _directory, _jobQueue);
  }

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_TwoOrganizations_ShouldQueueOneChildEach()
  {
    var contoso = AddOrganization("1001", "contoso", admitted: true);
    var fabrikam = AddOrganization("2002", "fabrikam", admitted: true);
    _listed.Add(Connection("contoso-devops", contoso.Id));
    _listed.Add(Connection("fabrikam-devops", fabrikam.Id));

    await _handler.Handle(new CheckConnectionsCommand(), CancellationToken.None);

    _queued.Count.ShouldBe(2);
    _queued.Select(static j => j.OrganizationId).ShouldBe([contoso.Id, fabrikam.Id]);
    _queued.ShouldAllBe(static j => j.Type == JobTypes.CheckConnection && !j.IsInstance && j.Coalesce);
    _queued[0].PayloadJson.ShouldBe(ConnectionHealthJobs.CheckPayload("contoso-devops"));
    _queued[0].LockKey.ShouldBe("check:connection:contoso-devops");
    _queued[0].DedupKey.ShouldBe("check:connection:contoso-devops");
    _queued[1].PayloadJson.ShouldBe(ConnectionHealthJobs.CheckPayload("fabrikam-devops"));
  }

  [Fact]
  public async Task Handle_FailingConnection_ShouldStillCheckIt()
  {
    var contoso = AddOrganization("1001", "contoso", admitted: true);
    var failing = Connection("contoso-devops", contoso.Id);
    failing.RecordCheck(Now, "revoked", TimeSpan.Zero);
    _listed.Add(failing);

    await _handler.Handle(new CheckConnectionsCommand(), CancellationToken.None);

    _queued.ShouldHaveSingleItem().PayloadJson.ShouldBe(ConnectionHealthJobs.CheckPayload("contoso-devops"));
  }

  [Fact]
  public async Task Handle_SuspendedConnection_ShouldSkipIt()
  {
    var contoso = AddOrganization("1001", "contoso", admitted: true);
    var suspended = Connection("contoso-old", contoso.Id);
    suspended.Suspend(Now);
    _listed.Add(suspended);
    _listed.Add(Connection("contoso-devops", contoso.Id));

    await _handler.Handle(new CheckConnectionsCommand(), CancellationToken.None);

    _queued.ShouldHaveSingleItem().LockKey.ShouldBe(ConnectionHealthJobs.CheckKey("contoso-devops"));
  }

  [Fact]
  public async Task Handle_OrganizationNotActive_ShouldSkipItsConnection()
  {
    var pending = AddOrganization("1001", "pending");
    var suspended = AddOrganization("2002", "suspended", admitted: true);
    suspended.Suspend(SuspensionReason.Administrator, Now);
    _listed.Add(Connection("pending-devops", pending.Id));
    _listed.Add(Connection("suspended-devops", suspended.Id));

    await _handler.Handle(new CheckConnectionsCommand(), CancellationToken.None);

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NoConnection_ShouldQueueNothing()
  {
    AddOrganization("1001", "contoso", admitted: true);

    await _handler.Handle(new CheckConnectionsCommand(), CancellationToken.None);

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NullCommand_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, CancellationToken.None));
  }

  private static ForgeConnection Connection(string id, long organizationId) =>
    ForgeConnection.Create(
      id,
      organizationId,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      $"account-{id}",
      new Uri("https://dev.azure.com/contoso"),
      Guid.Parse("11111111-2222-3333-4444-555555555555"),
      null,
      "protected:credential",
      "protected:secret",
      null,
      Now
    );

  private Organization AddOrganization(string accountId, string login, bool admitted = false)
  {
    var organization = Organization.CreatePending(Forge.AzureDevOps, accountId, login, OrganizationAccountType.Organization, Now);

    if (admitted)
    {
      organization.Admit("tests", Now);
    }

    return _directory.Add(organization);
  }

  #endregion
}
