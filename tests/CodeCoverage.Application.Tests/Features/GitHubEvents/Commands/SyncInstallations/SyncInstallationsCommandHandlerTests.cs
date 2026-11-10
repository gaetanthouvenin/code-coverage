// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallations;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.SyncInstallations;

public sealed class SyncInstallationsCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly IGitHubAppGateway _gitHub = A.Fake<IGitHubAppGateway>();

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly List<JobRequest> _queued = [];

  private readonly SyncInstallationsCommandHandler _handler;

  #endregion

  #region Ctors

  public SyncInstallationsCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _installations.FindOwnerAsync(A<long>._, A<CancellationToken>._)).Returns((InstallationOwner?)null);
    A.CallTo(() => _directory.FindByAccountIdAsync(A<Forge>._, A<string>._, A<CancellationToken>._)).Returns((Organization?)null);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);

    _handler = new SyncInstallationsCommandHandler(_gitHub, _installations, _directory, _jobQueue, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldQueueOneChildPerActiveOrganization()
  {
    Listed(new GitHubInstallationInfo(42, "1001", "first", false), new GitHubInstallationInfo(43, "2002", "second", false));
    Owned(42, 9, OrganizationState.Active);
    Owned(43, 10, OrganizationState.Active);

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _queued.Count.ShouldBe(2);
    _queued[0].OrganizationId.ShouldBe(9);
    _queued[0].Type.ShouldBe(JobTypes.SyncInstallationRepositories);
    _queued[0].PayloadJson.ShouldBe("""{"InstallationId":42}""");
    _queued[0].LockKey.ShouldBe("installation:42");
    _queued[0].DedupKey.ShouldBe("sync:installation:42");
    _queued[0].Coalesce.ShouldBeTrue();
    _queued[1].OrganizationId.ShouldBe(10);
    _queued[1].PayloadJson.ShouldBe("""{"InstallationId":43}""");
  }

  [Theory]
  [InlineData(OrganizationState.Pending)]
  [InlineData(OrganizationState.Suspended)]
  public async Task Handle_OrganizationNotActive_ShouldQueueNothing(OrganizationState state)
  {
    Listed(new GitHubInstallationInfo(42, "1001", "first", false));
    Owned(42, 9, state);

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_SuspendedInstallation_ShouldBeSkippedWithoutAnyReading()
  {
    Listed(new GitHubInstallationInfo(42, "1001", "first", true));
    Owned(42, 9, OrganizationState.Active);

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _queued.ShouldBeEmpty();
    A.CallTo(_installations).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownAccount_ShouldRecordNothing()
  {
    Listed(new GitHubInstallationInfo(42, "1001", "stranger", false));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _queued.ShouldBeEmpty();
    A.CallTo(() => _installations.AddAsync(A<Installation>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NewInstallationOfAKnownAccount_ShouldRecordItUnderTheOrganizationOfThatAccount()
  {
    var organization = Admitted("1001", "first");
    Listed(new GitHubInstallationInfo(42, "1001", "first", false));
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, "1001", A<CancellationToken>._)).Returns(organization);

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    A.CallTo(
       () => _installations.AddAsync(
         A<Installation>.That.Matches(i => i.Id == 42 && i.OrganizationId == organization.Id && i.CreatedAt == Now),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
    _queued.ShouldHaveSingleItem().OrganizationId.ShouldBe(organization.Id);
  }

  [Fact]
  public async Task Handle_NewInstallationOfAPendingAccount_ShouldRecordItAndQueueNothing()
  {
    var organization = WithId(Organization.CreatePending(Forge.GitHub, "1001", "first", OrganizationAccountType.Organization, Now));
    Listed(new GitHubInstallationInfo(42, "1001", "first", false));
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, "1001", A<CancellationToken>._)).Returns(organization);

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    A.CallTo(() => _installations.AddAsync(A<Installation>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_InstallationRecordedInTheMeantime_ShouldFollowItsRow()
  {
    Listed(new GitHubInstallationInfo(42, "1001", "first", false));
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, "1001", A<CancellationToken>._)).Returns(Admitted("1001", "first"));
    A.CallTo(() => _installations.AddAsync(A<Installation>._, A<CancellationToken>._))
     .Throws(new ConflictException(GeneralErrors.ConcurrentChange, "added in the meantime"));
    A.CallTo(() => _installations.FindOwnerAsync(42, A<CancellationToken>._))
     .ReturnsNextFromSequence(null, new InstallationOwner(77, OrganizationState.Active));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _queued.ShouldHaveSingleItem().OrganizationId.ShouldBe(77);
  }

  [Fact]
  public async Task Handle_NoInstallation_ShouldQueueNothing()
  {
    Listed();

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_Transfer_ShouldQueueTheClosingInTheActiveOrigin()
  {
    var origin = Admitted("2002", "origin");
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, "2002", A<CancellationToken>._)).Returns(origin);

    await _handler.Handle(new SyncInstallationsCommand("7", "2002"), Ct);

    var job = _queued.ShouldHaveSingleItem();
    job.OrganizationId.ShouldBe(origin.Id);
    job.Type.ShouldBe(JobTypes.ReconcileRepositoryTransfer);
    job.PayloadJson.ShouldBe("""{"RepositoryExternalId":"7"}""");
    job.LockKey.ShouldBe("repo:7");
    job.DedupKey.ShouldBe("transfer:7");
    // A transfer names one account: the installations are not listed again.
    A.CallTo(_gitHub).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_TransferFromAnAccountThatIsNotAnActiveOrganization_ShouldQueueNothing(bool known)
  {
    if (known)
    {
      A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, "2002", A<CancellationToken>._))
       .Returns(Organization.CreatePending(Forge.GitHub, "2002", "origin", OrganizationAccountType.User, Now));
    }

    await _handler.Handle(new SyncInstallationsCommand("7", "2002"), Ct);

    _queued.ShouldBeEmpty();
    A.CallTo(_gitHub).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("7", null)]
  [InlineData(null, "2002")]
  [InlineData("", "2002")]
  public async Task Handle_HalfATransfer_ShouldReconcileEveryInstallation(string? repository, string? owner)
  {
    Listed();

    await _handler.Handle(new SyncInstallationsCommand(repository, owner), Ct);

    A.CallTo(() => _gitHub.ListInstallationsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static Organization Admitted(string accountId, string login)
  {
    var organization = Organization.CreatePending(Forge.GitHub, accountId, login, OrganizationAccountType.Organization, Now);

    organization.Admit("tests", Now);

    return WithId(organization);
  }

  // The identifier is given by the database: a row read from it always has one.
  private static Organization WithId(Organization organization)
  {
    typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(organization, 12L);

    return organization;
  }

  private void Listed(params GitHubInstallationInfo[] installations)
  {
    A.CallTo(() => _gitHub.ListInstallationsAsync(A<CancellationToken>._)).Returns(installations);
  }

  private void Owned(long installationId, long organizationId, OrganizationState state)
  {
    A.CallTo(() => _installations.FindOwnerAsync(installationId, A<CancellationToken>._)).Returns(new InstallationOwner(organizationId, state));
  }

  #endregion
}