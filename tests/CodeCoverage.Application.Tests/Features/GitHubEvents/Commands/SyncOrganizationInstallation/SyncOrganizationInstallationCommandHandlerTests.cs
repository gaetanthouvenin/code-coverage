// ------------------------------------------------------------------------------------------------
// <copyright file="SyncOrganizationInstallationCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.GitHubEvents;
using CodeCoverage.Application.Features.GitHubEvents.Commands.SyncOrganizationInstallation;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.SyncOrganizationInstallation;

public sealed class SyncOrganizationInstallationCommandHandlerTests
{
  #region Fields

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IJobQueue _jobs = A.Fake<IJobQueue>();

  private readonly List<JobRequest> _queued = [];

  #endregion

  #region Ctors

  public SyncOrganizationInstallationCommandHandlerTests()
  {
    A.CallTo(() => _organization.Id).Returns(7L);
    A.CallTo(() => _jobs.TryEnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        _queued.Add(call.GetArgument<JobRequest>(0)!);

        return Task.FromResult(true);
      });
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Every installation of the organization is read again, with the key the webhooks already use.
  [Fact]
  public async Task Handle_ShouldQueueOneJobPerInstallationOfTheOrganization()
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(7, A<CancellationToken>._)).Returns([11L, 12L]);

    await new SyncOrganizationInstallationCommandHandler(_installations, _organization, _jobs).Handle(Command(), Ct);

    _queued.Select(static j => j.Type).ShouldAllBe(t => t == JobTypes.SyncInstallationRepositories);
    _queued.Select(static j => j.DedupKey).ShouldBe(["sync:installation:11", "sync:installation:12"]);
    _queued.ShouldAllBe(j => j.Coalesce);
  }

  // Critical rule 3: the request queues, and calls no forge.
  [Fact]
  public async Task Handle_ShouldOnlyQueue()
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(7, A<CancellationToken>._)).Returns([11L]);

    await new SyncOrganizationInstallationCommandHandler(_installations, _organization, _jobs).Handle(Command(), Ct);

    _queued.ShouldHaveSingleItem();
    A.CallTo(() => _jobs.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Decision of the readiness check: without an installation, the refusal is explicit, never a silent no-op.
  [Fact]
  public async Task Handle_WithoutInstallation_ShouldRefuse()
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(7, A<CancellationToken>._)).Returns([]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await new SyncOrganizationInstallationCommandHandler(_installations, _organization, _jobs).Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(GitHubEventErrors.NoInstallation);
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public void Command_ShouldRequireAnOrganizationAdministrator() => Command().RequiredRole.ShouldBe(OrganizationRole.Admin);

  private static SyncOrganizationInstallationCommand Command() => new(Forge.GitHub, "acme-labs");

  #endregion
}
