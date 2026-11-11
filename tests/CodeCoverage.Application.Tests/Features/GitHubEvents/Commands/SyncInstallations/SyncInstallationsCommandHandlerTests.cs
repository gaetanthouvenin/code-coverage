// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallations;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Support;
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

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly List<JobRequest> _queued = [];

  private readonly List<Installation> _recorded = [];

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly SyncInstallationsCommandHandler _handler;

  #endregion

  #region Ctors

  public SyncInstallationsCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _installations.FindOwnerAsync(A<long>._, A<CancellationToken>._)).Returns((InstallationOwner?)null);
    A.CallTo(() => _installations.AddAsync(A<Installation>._, A<CancellationToken>._))
     .Invokes((Installation installation, CancellationToken _) => _recorded.Add(installation));
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(A<long>._, A<CancellationToken>._)).Returns([99L]);
    // An installation GitHub left out of its listing is asked for on its own: by default it is gone.
    A.CallTo(() => _gitHub.GetInstallationAsync(A<long>._, A<CancellationToken>._)).Returns((GitHubInstallationInfo?)null);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);

    _handler = new SyncInstallationsCommandHandler(
      _gitHub,
      _installations,
      _directory,
      _jobQueue,
      new InstallationLifecycle(_directory, _installations, _auditLog, clock)
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  // What is queued besides the rewrite of the owner, which every active organization gets.
  private List<JobRequest> Children => [.. _queued.Where(static j => j.Type != JobTypes.RewriteRepositoryOwner)];

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldQueueOneChildPerActiveOrganization()
  {
    var first = _directory.Add("1001", "first", admitted: true);
    var second = _directory.Add("2002", "second", admitted: true);
    Listed(Installed(42, "1001", "first"), Installed(43, "2002", "second"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    Children.Count.ShouldBe(2);
    Children[0].OrganizationId.ShouldBe(first.Id);
    Children[0].Type.ShouldBe(JobTypes.SyncInstallationRepositories);
    Children[0].PayloadJson.ShouldBe("""{"InstallationId":42}""");
    Children[0].LockKey.ShouldBe("installation:42");
    Children[0].DedupKey.ShouldBe("sync:installation:42");
    Children[0].Coalesce.ShouldBeTrue();
    Children[1].OrganizationId.ShouldBe(second.Id);
    Children[1].PayloadJson.ShouldBe("""{"InstallationId":43}""");
  }

  // F19, S10. A rename learned here reaches the repositories GitHub no longer lists, inactive ones included: the
  // organization gets the rewrite of the owner of its repositories, which reads its login when it runs. An
  // organization whose login did not change gets none.
  [Fact]
  public async Task Handle_ShouldQueueTheRewriteOfTheOwnerOfAnActiveOrganizationRenamedOnly()
  {
    var renamed = _directory.Add("1001", "former-login", admitted: true);
    var pending = _directory.Add("2002", "pending");
    var unchanged = _directory.Add("3003", "same-login", admitted: true);
    Listed(Installed(42, "1001", "new-login"), Installed(43, "2002", "pending"), Installed(44, "3003", "same-login"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    renamed.Login.ShouldBe("new-login");
    var rewrite = _queued.Where(static j => j.Type == JobTypes.RewriteRepositoryOwner).ShouldHaveSingleItem();
    rewrite.OrganizationId.ShouldBe(renamed.Id);
    rewrite.PayloadJson.ShouldBe("{}");
    rewrite.LockKey.ShouldBe(RewriteRepositoryOwnerJobs.Key);
    rewrite.DedupKey.ShouldBe(RewriteRepositoryOwnerJobs.Key);
    rewrite.Coalesce.ShouldBeTrue();
    _queued.ShouldNotContain(j => j.OrganizationId == pending.Id);
    _queued.ShouldNotContain(j => j.OrganizationId == unchanged.Id && j.Type == JobTypes.RewriteRepositoryOwner);
  }

  [Fact]
  public async Task Handle_UnknownInstallation_ShouldCreatePendingOrganization()
  {
    Listed(Installed(42, "1001", "Stranger", OrganizationAccountType.User));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    // The delivery of installation.created was lost: the account waits for its admission, and nothing is tracked.
    var organization = _directory.All.ShouldHaveSingleItem();
    organization.State.ShouldBe(OrganizationState.Pending);
    organization.ExternalAccountId.ShouldBe("1001");
    organization.Login.ShouldBe("Stranger");
    organization.AccountType.ShouldBe(OrganizationAccountType.User);
    organization.AdmittedBy.ShouldBeNull();
    var installation = _recorded.ShouldHaveSingleItem();
    installation.Id.ShouldBe(42);
    installation.OrganizationId.ShouldBe(organization.Id);
    installation.CreatedAt.ShouldBe(Now);
    Children.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_PendingOrganization_ShouldStayPendingAndQueueNothing()
  {
    var organization = _directory.Add("1001", "first");
    Listed(Installed(42, "1001", "first"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.State.ShouldBe(OrganizationState.Pending);
    _recorded.ShouldHaveSingleItem().OrganizationId.ShouldBe(organization.Id);
    Children.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_RenamedAccount_ShouldUpdateTheLoginByAccountId()
  {
    var organization = _directory.Add("1001", "old-name", admitted: true);
    Listed(Installed(42, "1001", "New-Name"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.Login.ShouldBe("New-Name");
    _directory.All.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Handle_KnownInstallation_ShouldNotRecordItAgain()
  {
    _directory.Add("1001", "first", admitted: true);
    A.CallTo(() => _installations.FindOwnerAsync(42, A<CancellationToken>._)).Returns(new InstallationOwner(100, OrganizationState.Active));
    Listed(Installed(42, "1001", "first"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _recorded.ShouldBeEmpty();
    Children.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Handle_InstallationRecordedInTheMeantime_ShouldGoOn()
  {
    var organization = _directory.Add("1001", "first", admitted: true);
    Listed(Installed(42, "1001", "first"));
    A.CallTo(() => _installations.AddAsync(A<Installation>._, A<CancellationToken>._))
     .Throws(new ConflictException(GeneralErrors.ConcurrentChange, "added in the meantime"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    Children.ShouldHaveSingleItem().OrganizationId.ShouldBe(organization.Id);
  }

  [Fact]
  public async Task Handle_SuspendedInstallation_ShouldSuspendItsOrganizationAndQueueNothing()
  {
    var organization = _directory.Add("1001", "first", admitted: true);
    Listed(Installed(42, "1001", "first", suspended: true));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    // The delivery of installation.suspend was lost. GitHub refuses every call made for this installation.
    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.AppUninstalled);
    Children.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_InstallationNoLongerListed_ShouldSuspendItsOrganization()
  {
    var gone = _directory.Add("1001", "gone", admitted: true);
    var served = _directory.Add("2002", "served", admitted: true);
    Listed(Installed(43, "2002", "served"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    // The delivery of installation.deleted was lost.
    gone.State.ShouldBe(OrganizationState.Suspended);
    gone.SuspensionReason.ShouldBe(SuspensionReason.AppUninstalled);
    served.State.ShouldBe(OrganizationState.Active);
    Children.ShouldHaveSingleItem().OrganizationId.ShouldBe(served.Id);
  }

  // F21. The pages of the listing are numbered: an installation added or removed between two pages shifts the next
  // one, and an installation may be left out. Before it is suspended, its installation is asked for on its own.
  [Fact]
  public async Task Handle_InstallationLeftOutOfAShiftedListing_ShouldNotBeSuspended()
  {
    var shifted = _directory.Add("1001", "shifted", admitted: true);
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(shifted.Id, A<CancellationToken>._)).Returns([42L]);
    A.CallTo(() => _gitHub.GetInstallationAsync(42, A<CancellationToken>._)).Returns(Installed(42, "1001", "shifted"));
    Listed();

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    shifted.State.ShouldBe(OrganizationState.Active);
  }

  // F21. Asked on its own, the installation is suspended or gone: the organization is suspended.
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Handle_InstallationConfirmedGone_ShouldSuspendTheOrganization(bool suspendedOnGitHub)
  {
    var gone = _directory.Add("1001", "gone", admitted: true);
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(gone.Id, A<CancellationToken>._)).Returns([42L]);
    A.CallTo(() => _gitHub.GetInstallationAsync(42, A<CancellationToken>._))
     .Returns(suspendedOnGitHub ? Installed(42, "1001", "gone", suspended: true) : null);
    Listed();

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    gone.State.ShouldBe(OrganizationState.Suspended);
  }

  // D. A failure in the middle of the listing suspends nothing: the organizations the listing had not reached yet are
  // not taken for uninstalled.
  [Fact]
  public async Task Handle_FailureInTheMiddleOfTheListing_ShouldSuspendNothing()
  {
    _directory.Add("1001", "first", admitted: true);
    _directory.Add("2002", "second", admitted: true);
    var notListed = _directory.Add("3003", "not-listed", admitted: true);
    A.CallTo(() => _installations.FindOwnerAsync(43, A<CancellationToken>._)).Throws(new InvalidOperationException("The database does not answer."));
    Listed(Installed(42, "1001", "first"), Installed(43, "2002", "second"));

    await Should.ThrowAsync<InvalidOperationException>(async () => await _handler.Handle(new SyncInstallationsCommand(), Ct));

    notListed.State.ShouldBe(OrganizationState.Active);
    _directory.All.ShouldAllBe(static o => o.State == OrganizationState.Active);
  }

  // F18. The listing tells that an active account took a reserved login: suspended, and audited.
  [Fact]
  public async Task Handle_AccountRenamedToAReservedLogin_ShouldSuspendWithAnAuditEntry()
  {
    var organization = _directory.Add("1001", "octo", admitted: true);
    Listed(Installed(42, "1001", "admin"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.SuspensionReason.ShouldBe(SuspensionReason.ReservedLogin);
    A.CallTo(() => _auditLog.RecordAsync("Organization.Suspend", "GitHub/admin", A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ActiveOrganizationThatNeverHadAnInstallation_ShouldStayActive()
  {
    // Admitted from the command line before the account installed the application.
    var organization = _directory.Add("1001", "early", admitted: true);
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(organization.Id, A<CancellationToken>._)).Returns([]);
    Listed();

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.State.ShouldBe(OrganizationState.Active);
  }

  [Fact]
  public async Task Handle_OrganizationOfAnotherForge_ShouldNeverBeSuspendedByThisListing()
  {
    var organization = Organization.CreatePending(Forge.GitLab, "77", "elsewhere", OrganizationAccountType.Organization, Now);
    organization.Admit("tests", Now);
    _directory.Add(organization);
    Listed();

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.State.ShouldBe(OrganizationState.Active);
  }

  [Fact]
  public async Task Handle_ListingFails_ShouldChangeNoState()
  {
    var organization = _directory.Add("1001", "first", admitted: true);
    A.CallTo(() => _gitHub.ListInstallationsAsync(A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "GitHub lists more installations than this instance reads."));

    await Should.ThrowAsync<BadGatewayException>(async () => await _handler.Handle(new SyncInstallationsCommand(), Ct));

    organization.State.ShouldBe(OrganizationState.Active);
    _directory.Upserts.ShouldBe(0);
    Children.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_UninstalledOrganizationListedAgain_ShouldReactivateAndQueueItsChild()
  {
    var organization = _directory.Add("1001", "first", admitted: true);
    organization.Suspend(SuspensionReason.AppUninstalled, Now);
    Listed(Installed(44, "1001", "first"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    // The delivery of the reinstallation was lost.
    organization.State.ShouldBe(OrganizationState.Active);
    organization.SuspensionReason.ShouldBeNull();
    _recorded.ShouldHaveSingleItem().Id.ShouldBe(44);
    Children.ShouldHaveSingleItem().OrganizationId.ShouldBe(organization.Id);
  }

  [Fact]
  public async Task Handle_UninstalledBeforeItsAdmission_ShouldGoBackToPendingNotActive()
  {
    var organization = _directory.Add("1001", "first");
    organization.Suspend(SuspensionReason.AppUninstalled, Now);
    Listed(Installed(44, "1001", "first"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.State.ShouldBe(OrganizationState.Pending);
    Children.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(SuspensionReason.Administrator)]
  [InlineData(SuspensionReason.ReservedLogin)]
  public async Task Handle_SuspendedForAnotherReason_ShouldStaySuspended(SuspensionReason reason)
  {
    var organization = _directory.Add("1001", "first", admitted: true);
    organization.Suspend(reason, Now);
    Listed(Installed(42, "1001", "first"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(reason);
    Children.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("")]
  [InlineData("~stale-1001")]
  [InlineData("not/a/login")]
  public async Task Handle_AccountThatCannotBeAnOrganization_ShouldBeLeftOutAndTheOthersFollowed(string login)
  {
    var other = _directory.Add("2002", "second", admitted: true);
    Listed(Installed(42, "1001", login), Installed(43, "2002", "second"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    _directory.All.ShouldHaveSingleItem();
    Children.ShouldHaveSingleItem().OrganizationId.ShouldBe(other.Id);
  }

  [Fact]
  public async Task Handle_ListedAccountThatCannotBeFollowed_ShouldNotBeTakenForUninstalled()
  {
    var organization = _directory.Add("1001", "first", admitted: true);
    Listed(Installed(42, "1001", string.Empty));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    organization.State.ShouldBe(OrganizationState.Active);
  }

  [Fact]
  public async Task Handle_NoInstallation_ShouldQueueNothing()
  {
    Listed();

    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    Children.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_Twice_ShouldEndInTheSameState()
  {
    var gone = _directory.Add("1001", "gone", admitted: true);
    Listed(Installed(43, "2002", "new"));

    await _handler.Handle(new SyncInstallationsCommand(), Ct);
    var version = gone.Version;
    await _handler.Handle(new SyncInstallationsCommand(), Ct);

    gone.Version.ShouldBe(version);
    _directory.All.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_Transfer_ShouldQueueTheClosingInTheActiveOrigin()
  {
    var origin = _directory.Add("2002", "origin", admitted: true);

    await _handler.Handle(new SyncInstallationsCommand("7", "2002"), Ct);

    var job = Children.ShouldHaveSingleItem();
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
      _directory.Add("2002", "origin", accountType: OrganizationAccountType.User);
    }

    await _handler.Handle(new SyncInstallationsCommand("7", "2002"), Ct);

    Children.ShouldBeEmpty();
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

  private static GitHubInstallationInfo Installed(
    long id,
    string accountId,
    string login,
    OrganizationAccountType accountType = OrganizationAccountType.Organization,
    bool suspended = false) =>
    new(id, accountId, login, accountType, suspended);

  private void Listed(params GitHubInstallationInfo[] installations)
  {
    A.CallTo(() => _gitHub.ListInstallationsAsync(A<CancellationToken>._)).Returns(installations);
  }

  #endregion
}
