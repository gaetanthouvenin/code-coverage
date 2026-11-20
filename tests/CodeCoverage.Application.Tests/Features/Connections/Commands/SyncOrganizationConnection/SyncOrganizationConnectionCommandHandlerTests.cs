// ------------------------------------------------------------------------------------------------
// <copyright file="SyncOrganizationConnectionCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Connections;
using CodeCoverage.Application.Features.Connections.Commands.SyncOrganizationConnection;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.SyncOrganizationConnection;

public sealed class SyncOrganizationConnectionCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IJobQueue _jobs = A.Fake<IJobQueue>();

  private readonly List<JobRequest> _queued = [];

  private readonly SyncOrganizationConnectionCommandHandler _handler;

  #endregion

  #region Ctors

  public SyncOrganizationConnectionCommandHandlerTests()
  {
    A.CallTo(() => _organization.Id).Returns(7L);
    A.CallTo(() => _jobs.TryEnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        _queued.Add(call.GetArgument<JobRequest>(0)!);

        return Task.FromResult(true);
      });

    _handler = new SyncOrganizationConnectionCommandHandler(_installations, _connections, _organization, _jobs);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Every installation of the organization is read again, with the key the webhooks already use.
  [Fact]
  public async Task Handle_GitHubInstallations_ShouldQueueOneJobPerInstallation()
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(7, A<CancellationToken>._)).Returns([11L, 12L]);

    await _handler.Handle(Command(Forge.GitHub), Ct);

    _queued.Select(static j => j.Type).ShouldAllBe(t => t == JobTypes.SyncConnectionRepositories);
    _queued.Select(static j => j.DedupKey).ShouldBe(["sync:installation:11", "sync:installation:12"]);
    _queued.ShouldAllBe(j => j.Coalesce);
    A.CallTo(_connections).MustNotHaveHappened();
  }

  // Every connection that is not suspended is read again, with the key of the periodic run.
  [Fact]
  public async Task Handle_AzureDevOpsConnection_ShouldQueueTheSync()
  {
    var suspended = Connection("conn-b");
    suspended.Suspend(Now);
    A.CallTo(() => _connections.ListForOrganizationAsync(7, A<CancellationToken>._)).Returns([Connection("conn-a"), suspended]);

    await _handler.Handle(Command(Forge.AzureDevOps), Ct);

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.SyncConnectionRepositories);
    job.DedupKey.ShouldBe("sync:connection:conn-a");
    job.PayloadJson.ShouldBe(RepositoryReconciliationJobs.ConnectionPayload(Forge.AzureDevOps, "conn-a"));
    job.Coalesce.ShouldBeTrue();
    A.CallTo(_installations).MustNotHaveHappened();
  }

  // Critical rule 3: the request queues, and calls no forge.
  [Fact]
  public async Task Handle_ShouldOnlyQueue()
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(7, A<CancellationToken>._)).Returns([11L]);

    await _handler.Handle(Command(Forge.GitHub), Ct);

    _queued.ShouldHaveSingleItem();
    A.CallTo(() => _jobs.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Decision of the readiness check: without an installation, the refusal is explicit, never a silent no-op.
  [Fact]
  public async Task Handle_NoInstallation_ShouldThrowNotConnected()
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(7, A<CancellationToken>._)).Returns([]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await _handler.Handle(Command(Forge.GitHub), Ct));

    exception.ErrorCode.ShouldBe(ConnectionErrors.NotConnected);
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NoConnection_ShouldThrowNotConnected()
  {
    A.CallTo(() => _connections.ListForOrganizationAsync(7, A<CancellationToken>._)).Returns([]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await _handler.Handle(Command(Forge.AzureDevOps), Ct));

    exception.ErrorCode.ShouldBe(ConnectionErrors.NotConnected);
    _queued.ShouldBeEmpty();
  }

  // A suspended connection reaches nothing on its forge: it counts as no connection.
  [Fact]
  public async Task Handle_OnlySuspendedConnections_ShouldThrowNotConnected()
  {
    var suspended = Connection("conn-a");
    suspended.Suspend(Now);
    A.CallTo(() => _connections.ListForOrganizationAsync(7, A<CancellationToken>._)).Returns([suspended]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await _handler.Handle(Command(Forge.AzureDevOps), Ct));

    exception.ErrorCode.ShouldBe(ConnectionErrors.NotConnected);
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  [Fact]
  public void ThrowIfNotConnected_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => UnprocessableEntityException.ThrowIfNotConnected<long>(null!, Forge.GitHub));
  }

  [Fact]
  public void Command_ShouldRequireAnOrganizationAdministrator() => Command(Forge.GitHub).RequiredRole.ShouldBe(OrganizationRole.Admin);

  private static SyncOrganizationConnectionCommand Command(Forge forge) => new(forge, "acme-labs");

  private static ForgeConnection Connection(string id) =>
    ForgeConnection.Create(
      id,
      7,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      "contoso",
      new Uri("https://dev.azure.com/contoso"),
      Guid.Parse("11111111-2222-3333-4444-555555555555"),
      null,
      "protected:credential",
      "protected:secret",
      null,
      Now
    );

  #endregion
}
