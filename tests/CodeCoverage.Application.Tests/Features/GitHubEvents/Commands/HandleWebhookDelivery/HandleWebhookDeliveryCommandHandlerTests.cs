// ------------------------------------------------------------------------------------------------
// <copyright file="HandleWebhookDeliveryCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.GitHubEvents;
using CodeCoverage.Application.Features.GitHubEvents.Commands.HandleWebhookDelivery;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.HandleWebhookDelivery;

public sealed class HandleWebhookDeliveryCommandHandlerTests
{
  #region Fields

  private const long Installation = 42;

  private const long RepositoryId = 5;

  private const string AccountId = "1001";

  private const string RepositoryBody = """{"id":7,"name":"New-Name","private":true,"default_branch":"trunk","owner":{"login":"octo","id":1001}}""";

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha PushedHead = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private readonly IWebhookDeliveryRepository _deliveries = A.Fake<IWebhookDeliveryRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly List<Installation> _recorded = [];

  private readonly ICurrentOrganization _currentOrganization = A.Fake<ICurrentOrganization>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly List<JobRequest> _queued = [];

  private readonly HandleWebhookDeliveryCommandHandler _handler;

  private JobRequest? _savedWith;

  #endregion

  #region Ctors

  public HandleWebhookDeliveryCommandHandlerTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var generator = A.Fake<ITokenGenerator>();
    var protector = A.Fake<ISecretProtector>();

    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _currentOrganization.IsKnown).Returns(true);
    A.CallTo(() => _currentOrganization.Id).Returns(9);
    _directory.Current = _directory.Add(AccountId, "octo", admitted: true);
    A.CallTo(() => _installations.FindOwnerAsync(A<long>._, A<CancellationToken>._)).Returns((InstallationOwner?)null);
    A.CallTo(() => _installations.AddAsync(A<Installation>._, A<CancellationToken>._))
     .Invokes((Installation installation, CancellationToken _) => _recorded.Add(installation));
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => generator.NewBadgeToken()).Returns("new-badge");
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => $"protected:{value}");
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);
    A.CallTo(() => _repositories.SaveWithJobAsync(A<TrackedRepository>._, A<JobRequest>._, A<CancellationToken>._))
     .Invokes((TrackedRepository _, JobRequest job, CancellationToken _) => _savedWith = job);
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .Returns(new RepositoryDepartureTokens(2, 1));

    var reconciler = new RepositoryReconciler(_repositories, _directory, gateways, generator, protector, _auditLog, _clock);

    _handler = new HandleWebhookDeliveryCommandHandler(
      _deliveries,
      _repositories,
      _directory,
      _installations,
      _currentOrganization,
      _jobQueue,
      reconciler,
      new InstallationLifecycle(_directory, _installations, _auditLog, _clock),
      new PushProcessor(
        _branches,
        _commits,
        _repositories,
        new BranchHeadTracker(_commits, new CommitAncestry(A.Fake<IGitCommitRepository>(), gateways, _clock)),
        new ConfigResolver(A.Fake<ICommitConfigRepository>(), gateways, A.Fake<ICoverageConfigParser>(), A.Fake<IOrganizationSettingsProvider>(), _clock),
        gateways,
        _clock
      ),
      _clock
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_UnknownDelivery_ShouldThrowNotFound()
  {
    A.CallTo(() => _deliveries.FindForProcessingAsync("d-1", A<CancellationToken>._)).Returns((GitHubWebhookEvent?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await HandleAsync());

    exception.ErrorCode.ShouldBe(GitHubEventErrors.DeliveryNotFound);
    A.CallTo(_repositories).MustNotHaveHappened();
    A.CallTo(() => _deliveries.MarkProcessedAsync(A<string>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RepositoryRenamed_ShouldUpdateNameInsideOrganization()
  {
    var repository = Tracked("octo/old-name");
    Stored("repository", "renamed", $$"""{"action":"renamed","repository":{{RepositoryBody}}}""");

    await HandleAsync();

    repository.Name.ShouldBe("New-Name");
    repository.NormalizedName.ShouldBe("new-name");
    repository.DefaultBranch.ShouldBe("trunk");
    // Looked up by its identifier on GitHub, under the filter of the organization of the scope: never by its old name.
    A.CallTo(() => _repositories.FindByExternalIdAsync("7", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repositories.UpdateAsync(repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RepositoryRenamedNotTracked_ShouldCreateNothing()
  {
    NotTracked();
    Stored("repository", "renamed", $$"""{"action":"renamed","repository":{{RepositoryBody}}}""");

    await HandleAsync();

    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("privatized", true)]
  [InlineData("publicized", false)]
  public async Task Handle_RepositoryVisibilityChanged_ShouldFollowTheBody(string action, bool isPrivate)
  {
    var repository = Tracked("octo/New-Name", isPrivate: !isPrivate, defaultBranch: "trunk");
    var body = RepositoryBody.Replace("\"private\":true", isPrivate ? "\"private\":true" : "\"private\":false", StringComparison.Ordinal);
    Stored("repository", action, "{\"repository\":" + body + "}");

    await HandleAsync();

    repository.IsPrivate.ShouldBe(isPrivate);
    A.CallTo(() => _repositories.UpdateAsync(repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RepositoryUnchanged_ShouldNotSave()
  {
    Tracked("octo/New-Name", isPrivate: true, defaultBranch: "trunk");
    Stored("repository", "renamed", $$"""{"repository":{{RepositoryBody}}}""");

    await HandleAsync();

    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RepositoryDeleted_ShouldDeactivate()
  {
    var repository = Tracked("octo/repo");
    Stored("repository", "deleted", $$"""{"repository":{{RepositoryBody}}}""");

    await HandleAsync();

    repository.IsActive.ShouldBeFalse();
    A.CallTo(() => _repositories.UpdateAsync(repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("created")]
  [InlineData("archived")]
  [InlineData(null)]
  public async Task Handle_RepositoryOtherAction_ShouldChangeNothing(string? action)
  {
    var repository = Tracked("octo/repo");
    Stored("repository", action, $$"""{"repository":{{RepositoryBody}}}""");

    await HandleAsync();

    repository.Version.ShouldBe(0);
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RepositoryBodyWithoutRepository_ShouldChangeNothing()
  {
    Stored("repository", "renamed", """{"action":"renamed"}""");

    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RepositoryEventOnTheLaneOfTheInstance_ShouldReadNoRepository()
  {
    OnTheLaneOfTheInstance();
    Stored("repository", "renamed", $$"""{"repository":{{RepositoryBody}}}""");

    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_InstallationRepositoriesRemoved_ShouldDeactivate()
  {
    var repository = Tracked("octo/repo");
    Stored("installation_repositories", "removed", """{"action":"removed","repositories_removed":[{"id":7,"name":"repo"}]}""");

    await HandleAsync();

    repository.IsActive.ShouldBeFalse();
    A.CallTo(() => _repositories.UpdateAsync(repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    // Removed from the installation is not a transfer: its tokens are kept for the day it is selected again.
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_InstallationRepositoriesRemovedNotTracked_ShouldSaveNothing()
  {
    NotTracked();
    Stored("installation_repositories", "removed", """{"repositories_removed":[{"id":7}]}""");

    await HandleAsync();

    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("installation_repositories", "added")]
  [InlineData("installation_repositories", "removed")]
  [InlineData("installation", "created")]
  [InlineData("installation", "unsuspend")]
  public async Task Handle_InstallationEventInsideOrganization_ShouldQueueTheListingOfItsInstallation(string @event, string action)
  {
    NotTracked();
    Stored(@event, action, "{}");

    await HandleAsync();

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.SyncInstallationRepositories);
    // The organization of the scope, never one named by the body.
    job.OrganizationId.ShouldBeNull();
    job.IsInstance.ShouldBeFalse();
    job.PayloadJson.ShouldBe("""{"InstallationId":42}""");
  }

  [Theory]
  [InlineData("deleted")]
  [InlineData("suspend")]
  public async Task Handle_InstallationGone_ShouldSuspendTheOrganizationOfTheScopeAndQueueNoListing(string action)
  {
    // The body names another account: inside an organization, only the one of the scope is suspended.
    var other = _directory.Add("2002", "other", admitted: true);
    Stored("installation", action, InstallationBody(42, 2002, "other"));

    await HandleAsync();

    _directory.Current!.State.ShouldBe(OrganizationState.Suspended);
    _directory.Current.SuspensionReason.ShouldBe(SuspensionReason.AppUninstalled);
    other.State.ShouldBe(OrganizationState.Active);
    // GitHub answers nothing for this installation. The coordinator tells whether another one serves the account.
    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.SyncInstallations);
    job.IsInstance.ShouldBeTrue();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // F20. The account installed the application again before the removal of its former installation was handled: the
  // late delivery names an installation that is no longer the current one, and suspends nothing.
  [Theory]
  [InlineData("deleted")]
  [InlineData("suspend")]
  public async Task Handle_LateRemovalOfAFormerInstallation_ShouldNotSuspendTheOrganization(string action)
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(_directory.Current!.Id, A<CancellationToken>._)).Returns([50L, Installation]);
    Stored("installation", action, InstallationBody(Installation, 1001, "octo"));

    await HandleAsync();

    _directory.Current!.State.ShouldBe(OrganizationState.Active);
    _queued.ShouldBeEmpty();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("deleted")]
  [InlineData("suspend")]
  public async Task Handle_LateRemovalOfAFormerInstallationOnTheLaneOfTheInstance_ShouldNotSuspendTheOrganization(string action)
  {
    OnTheLaneOfTheInstance();
    var pending = _directory.Add("2002", "newcomer");
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(pending.Id, A<CancellationToken>._)).Returns([50L, Installation]);
    Stored("installation", action, InstallationBody(Installation, 2002, "newcomer"));

    await HandleAsync();

    pending.State.ShouldBe(OrganizationState.Pending);
  }

  // F20. The current installation is the most recent one recorded: its removal suspends.
  [Fact]
  public async Task Handle_RemovalOfTheCurrentInstallation_ShouldSuspendTheOrganization()
  {
    A.CallTo(() => _installations.ListIdsForOrganizationAsync(_directory.Current!.Id, A<CancellationToken>._)).Returns([Installation, 30L]);
    Stored("installation", "deleted", InstallationBody(Installation, 1001, "octo"));

    await HandleAsync();

    _directory.Current!.State.ShouldBe(OrganizationState.Suspended);
  }

  // F18. The account was renamed to a reserved login: the installation event that tells it suspends the organization,
  // and the suspension is audited as one an administrator decides.
  [Fact]
  public async Task Handle_InstallationCreatedUnderAReservedLogin_ShouldSuspendWithAnAuditEntry()
  {
    Stored("installation", "created", InstallationBody(Installation, 1001, "admin"));

    await HandleAsync();

    _directory.Current!.State.ShouldBe(OrganizationState.Suspended);
    _directory.Current.SuspensionReason.ShouldBe(SuspensionReason.ReservedLogin);
    ReservedLoginAudited("GitHub/admin");
  }

  // F18. Installed again under a login that became reserved meanwhile: it stays suspended for that reason, audited.
  [Fact]
  public async Task Handle_ReinstalledUnderAReservedLogin_ShouldStaySuspendedWithAnAuditEntry()
  {
    OnTheLaneOfTheInstance();
    var organization = _directory.Add("2002", "former", admitted: true);
    organization.Suspend(SuspensionReason.AppUninstalled, Now);
    Stored("installation", "created", InstallationBody(44, 2002, "admin"));

    await HandleAsync();

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.ReservedLogin);
    ReservedLoginAudited("GitHub/admin");
  }

  [Theory]
  [InlineData("deleted")]
  [InlineData("suspend")]
  public async Task Handle_InstallationGoneOnTheLaneOfTheInstance_ShouldSuspendTheAccountOfTheBody(string action)
  {
    OnTheLaneOfTheInstance();
    var pending = _directory.Add("2002", "newcomer");
    Stored("installation", action, InstallationBody(43, 2002, "newcomer"));

    await HandleAsync();

    pending.State.ShouldBe(OrganizationState.Suspended);
    pending.SuspensionReason.ShouldBe(SuspensionReason.AppUninstalled);
    _directory.Current!.State.ShouldBe(OrganizationState.Active);
  }

  [Fact]
  public async Task Handle_InstallationGoneForAnUnknownAccount_ShouldCreateNothing()
  {
    OnTheLaneOfTheInstance();
    Stored("installation", "deleted", InstallationBody(43, 2002, "stranger"));

    await HandleAsync();

    _directory.All.ShouldHaveSingleItem();
    _directory.Upserts.ShouldBe(0);
  }

  [Theory]
  [InlineData("installation", "created")]
  [InlineData("installation_repositories", "removed")]
  public async Task Handle_InstallationEventOnTheLaneOfTheInstance_ShouldQueueTheCoordinatorAndCreateNoRepository(string @event, string action)
  {
    OnTheLaneOfTheInstance();
    Stored(@event, action, """{"repositories_removed":[{"id":7}],"repositories_added":[{"id":8}]}""");

    await HandleAsync();

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.SyncInstallations);
    job.IsInstance.ShouldBeTrue();
    A.CallTo(_repositories).MustNotHaveHappened();
    _directory.Upserts.ShouldBe(0);
  }

  [Theory]
  [InlineData("Organization", OrganizationAccountType.Organization)]
  [InlineData("User", OrganizationAccountType.User)]
  public async Task Handle_InstallationCreated_ShouldCreatePendingOrganization(string type, OrganizationAccountType accountType)
  {
    OnTheLaneOfTheInstance();
    Stored("installation", "created", InstallationBody(43, 2002, "Newcomer", type));

    await HandleAsync();

    var organization = _directory.All.Single(o => o.ExternalAccountId == "2002");
    organization.State.ShouldBe(OrganizationState.Pending);
    organization.Login.ShouldBe("Newcomer");
    organization.AccountType.ShouldBe(accountType);
    organization.AdmittedBy.ShouldBeNull();
    // Linked by the identifier of its account, and nothing is tracked.
    var installation = _recorded.ShouldHaveSingleItem();
    installation.Id.ShouldBe(43);
    installation.OrganizationId.ShouldBe(organization.Id);
    A.CallTo(_repositories).MustNotHaveHappened();
    _queued.ShouldHaveSingleItem().Type.ShouldBe(JobTypes.SyncInstallations);
  }

  [Theory]
  [InlineData("created")]
  [InlineData("unsuspend")]
  public async Task Handle_ReinstallSameAccount_ShouldReattachAndReactivate(string action)
  {
    OnTheLaneOfTheInstance();
    var organization = _directory.Add("2002", "back-again", admitted: true);
    organization.Suspend(SuspensionReason.AppUninstalled, Now);
    Stored("installation", action, InstallationBody(44, 2002, "back-again"));

    await HandleAsync();

    organization.State.ShouldBe(OrganizationState.Active);
    organization.SuspensionReason.ShouldBeNull();
    _directory.All.Count.ShouldBe(2);
    var installation = _recorded.ShouldHaveSingleItem();
    installation.Id.ShouldBe(44);
    installation.OrganizationId.ShouldBe(organization.Id);
    // The repositories are listed by the coordinator, in the organization that is active again.
    _queued.ShouldHaveSingleItem().Type.ShouldBe(JobTypes.SyncInstallations);
  }

  [Fact]
  public async Task Handle_ReinstallBeforeAnyAdmission_ShouldGoBackToPendingNotActive()
  {
    OnTheLaneOfTheInstance();
    var organization = _directory.Add("2002", "never-admitted");
    organization.Suspend(SuspensionReason.AppUninstalled, Now);
    Stored("installation", "created", InstallationBody(44, 2002, "never-admitted"));

    await HandleAsync();

    organization.State.ShouldBe(OrganizationState.Pending);
    organization.AdmittedBy.ShouldBeNull();
  }

  [Theory]
  [InlineData(SuspensionReason.Administrator)]
  [InlineData(SuspensionReason.ReservedLogin)]
  public async Task Handle_AdminSuspended_ShouldStaySuspendedOnReinstall(SuspensionReason reason)
  {
    OnTheLaneOfTheInstance();
    var organization = _directory.Add("2002", "blocked", admitted: true);
    organization.Suspend(reason, Now);
    Stored("installation", "created", InstallationBody(44, 2002, "blocked"));

    await HandleAsync();

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(reason);
    // The installation is recorded all the same: the day the suspension is lifted, its deliveries find the organization.
    _recorded.ShouldHaveSingleItem().OrganizationId.ShouldBe(organization.Id);
  }

  [Fact]
  public async Task Handle_AdminSuspendedAfterTheRemoval_ShouldStaySuspendedOnReinstall()
  {
    OnTheLaneOfTheInstance();
    var organization = _directory.Add("2002", "blocked", admitted: true);
    organization.Suspend(SuspensionReason.AppUninstalled, Now);
    organization.Suspend(SuspensionReason.Administrator, Now);
    Stored("installation", "created", InstallationBody(44, 2002, "blocked"));

    await HandleAsync();

    organization.State.ShouldBe(OrganizationState.Suspended);
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("""{"installation":{"id":44}}""")]
  [InlineData("""{"installation":{"id":44,"account":{"id":2002}}}""")]
  [InlineData("""{"installation":{"id":44,"account":{"id":2002,"login":"~stale-2002"}}}""")]
  [InlineData("""{"installation":{"id":44,"account":{"id":2002,"slug":"an-enterprise"}}}""")]
  public async Task Handle_InstallationCreatedWithoutAnAccountThatCanBeAnOrganization_ShouldCreateNothing(string body)
  {
    OnTheLaneOfTheInstance();
    Stored("installation", "created", body);

    await HandleAsync();

    _directory.All.ShouldHaveSingleItem();
    _recorded.ShouldBeEmpty();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_InstallationCreatedTwice_ShouldChangeNothingMore()
  {
    OnTheLaneOfTheInstance();
    Stored("installation", "created", InstallationBody(43, 2002, "newcomer"));

    await HandleAsync();
    var organization = _directory.All.Single(o => o.ExternalAccountId == "2002");
    var version = organization.Version;
    A.CallTo(() => _installations.FindOwnerAsync(43, A<CancellationToken>._)).Returns(new InstallationOwner(organization.Id, organization.State));
    await HandleAsync();

    organization.Version.ShouldBe(version);
    _directory.All.Count.ShouldBe(2);
    _recorded.ShouldHaveSingleItem();
  }

  [Theory]
  [InlineData("new_permissions_accepted")]
  [InlineData(null)]
  public async Task Handle_InstallationOtherAction_ShouldChangeNoOrganization(string? action)
  {
    OnTheLaneOfTheInstance();
    Stored("installation", action, InstallationBody(43, 2002, "newcomer"));

    await HandleAsync();

    _directory.All.ShouldHaveSingleItem();
    _directory.Upserts.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_TransferredIn_ShouldCreateRowAndQueueTheReconciliationOfTheOrigin()
  {
    NotTracked();
    TrackedRepository? saved = null;
    A.CallTo(() => _repositories.SaveWithJobAsync(A<TrackedRepository>._, A<JobRequest>._, A<CancellationToken>._))
     .Invokes((TrackedRepository repository, JobRequest job, CancellationToken _) => (saved, _savedWith) = (repository, job));
    Stored("repository", "transferred", Transferred(ownerId: 1001, previousOwnerId: 2002));

    await HandleAsync();

    var repository = saved.ShouldNotBeNull();
    repository.ExternalId.ShouldBe("7");
    repository.InstallationId.ShouldBe(Installation);
    repository.Name.ShouldBe("New-Name");
    repository.IsActive.ShouldBeTrue();
    repository.BadgeTokenProtected.ShouldBe("protected:new-badge");

    var job = _savedWith.ShouldNotBeNull();
    job.Type.ShouldBe(JobTypes.SyncInstallations);
    job.IsInstance.ShouldBeTrue();
    job.PayloadJson.ShouldBe("""{"TransferredRepositoryId":"7","PreviousOwnerAccountId":"2002"}""");
    // Queued by the save of the row, in its transaction: never on its own.
    _queued.ShouldBeEmpty();
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_TransferredInWithoutPreviousOwner_ShouldQueueThePlainReconciliation()
  {
    NotTracked();
    Stored("repository", "transferred", $$"""{"repository":{{RepositoryBody}}}""");

    await HandleAsync();

    _savedWith.ShouldNotBeNull().DedupKey.ShouldBe("installations:event");
  }

  [Fact]
  public async Task Handle_TransferredInWithoutDefaultBranch_ShouldCreateNothing()
  {
    NotTracked();
    var body = RepositoryBody.Replace("\"default_branch\":\"trunk\",", string.Empty, StringComparison.Ordinal);
    Stored("repository", "transferred", "{\"repository\":" + body + "}");

    await HandleAsync();

    A.CallTo(() => _repositories.SaveWithJobAsync(A<TrackedRepository>._, A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_TransferredAway_ShouldRevokeRepositoryTokens()
  {
    var repository = Tracked("octo/repo");
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._)).Returns((ForgeRepositoryInfo?)null);
    Stored("repository", "transferred", Transferred(ownerId: 3003, previousOwnerId: 1001));

    await HandleAsync();

    repository.IsActive.ShouldBeFalse();
    repository.BadgeTokenProtected.ShouldBe("protected:new-badge");
    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repositories.SaveWithJobAsync(A<TrackedRepository>._, A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    Audited(RepositoryReconciler.RevokeTokensAuditAction, "2");
    Audited(RepositoryReconciler.AdviseRotationAuditAction, "1");
    A.CallTo(() => _auditLog.RecordAsync(RepositoryReconciler.RotateBadgeTokenAuditAction, "0", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_TransferredAwayToAnotherAccountStillReadable_ShouldClose()
  {
    var repository = Tracked("octo/repo");
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .Returns(new ForgeRepositoryInfo("7", "someone-else", string.Empty, "repo", "main", false));
    Stored("repository", "transferred", Transferred(ownerId: 3003, previousOwnerId: 1001));

    await HandleAsync();

    repository.IsActive.ShouldBeFalse();
    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_TransferredAwayButBackSince_ShouldKeepTheRow()
  {
    var repository = Tracked("octo/repo");
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .Returns(new ForgeRepositoryInfo("7", "OCTO", string.Empty, "repo", "main", false));
    Stored("repository", "transferred", Transferred(ownerId: 3003, previousOwnerId: 1001));

    await HandleAsync();

    repository.IsActive.ShouldBeTrue();
    repository.BadgeTokenProtected.ShouldBe("badge");
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_TransferredAwayNeverTracked_ShouldDoNothing()
  {
    NotTracked();
    Stored("repository", "transferred", Transferred(ownerId: 3003, previousOwnerId: 1001));

    await HandleAsync();

    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repositories.SaveWithJobAsync(A<TrackedRepository>._, A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_TransferredBack_ShouldNotRestoreTokens()
  {
    // The row the repository left: closed, its badge token replaced, its upload tokens revoked.
    var repository = Tracked("octo/repo");
    repository.Deactivate(Now.AddDays(-1));
    repository.RotateBadgeToken("rotated", Now.AddDays(-1));
    Stored("repository", "transferred", Transferred(ownerId: 1001, previousOwnerId: 3003));

    await HandleAsync();

    repository.IsActive.ShouldBeTrue();
    repository.InstallationId.ShouldBe(Installation);
    repository.Name.ShouldBe("New-Name");
    // The token replaced at the departure stays: the value that was public before the transfer reads nothing.
    repository.BadgeTokenProtected.ShouldBe("rotated");
    A.CallTo(() => _repositories.SaveWithJobAsync(repository, A<JobRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    // Nothing un-revokes an upload token: only the row comes back.
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
    _savedWith.ShouldNotBeNull().PayloadJson.ShouldBe("""{"TransferredRepositoryId":"7","PreviousOwnerAccountId":"3003"}""");
  }

  [Fact]
  public async Task Handle_Push_ShouldMoveTheHeadOfTheBranchOfATrackedRepository()
  {
    TrackedWithId("octo/repo");
    A.CallTo(() => _branches.FindAsync(RepositoryId, "feature/x", A<CancellationToken>._)).Returns((Branch?)null);
    Stored("push", null, Push("refs/heads/feature/x"));

    await HandleAsync();

    A.CallTo(
       () => _branches.AddAsync(
         A<Branch>.That.Matches(b => b.Name == "feature/x" && b.HeadSha == PushedHead && b.CoveredCommitId == null),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
    // The commit of the head is known from now on; no parent is ever read from the commits the body lists.
    A.CallTo(() => _commits.GetOrCreateAsync(RepositoryId, PushedHead, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_PushDeleted_ShouldRemoveBranch()
  {
    TrackedWithId("octo/repo");
    Stored("push", null, """{"ref":"refs/heads/gone","after":"0000000000000000000000000000000000000000","deleted":true,"head_commit":null}""");

    await HandleAsync();

    A.CallTo(() => _branches.RemoveAsync(RepositoryId, "gone", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _branches.AddAsync(A<Branch>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_commits).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_PushDefaultBranchYaml_ShouldRefresh()
  {
    var repository = TrackedWithId("octo/repo");
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, PushedHead, false, A<CancellationToken>._))
     .Returns(new ForgeTree(PushedHead, [new ForgeTreeEntry("codecov.yml", PushedHead, false)], false));
    A.CallTo(() => _gateway.GetContentAsync(A<ForgeRepositoryRef>._, "codecov.yml", PushedHead, A<CancellationToken>._)).Returns("comment: false\n");
    // The body only tells that the file changed: its content is never read from it.
    Stored("push", null, Push("refs/heads/main", """[{"id":"x","modified":["codecov.yml"],"content":"from the payload"}]"""));

    await HandleAsync();

    repository.DefaultBranchYaml.ShouldBe("comment: false\n");
    repository.DefaultBranchYamlSha.ShouldBe(PushedHead);
    A.CallTo(() => _repositories.UpdateAsync(repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_PushOnAnotherBranchTouchingTheConfiguration_ShouldNotRefreshTheDefault()
  {
    var repository = TrackedWithId("octo/repo");
    Stored("push", null, Push("refs/heads/feature/x", """[{"added":["codecov.yml"]}]"""));

    await HandleAsync();

    repository.DefaultBranchYaml.ShouldBeNull();
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_PushToARepositoryThatIsNotTracked_ShouldChangeNothing(bool known)
  {
    if (known)
    {
      TrackedWithId("octo/repo").Deactivate(Now);
    }
    else
    {
      NotTracked();
    }

    Stored("push", null, Push("refs/heads/main"));

    await HandleAsync();

    A.CallTo(_branches).MustNotHaveHappened();
    A.CallTo(_commits).MustNotHaveHappened();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_PushOfATag_ShouldChangeNothing()
  {
    TrackedWithId("octo/repo");
    Stored("push", null, Push("refs/tags/v1.0"));

    await HandleAsync();

    A.CallTo(_branches).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_PushOnTheLaneOfTheInstance_ShouldReadNoRepository()
  {
    OnTheLaneOfTheInstance();
    Stored("push", null, Push("refs/heads/main"));

    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
    A.CallTo(_branches).MustNotHaveHappened();
  }

  // A save that loses a race, a finalization that created the branch meanwhile, leaves the delivery unprocessed: the
  // job is retried.
  [Fact]
  public async Task Handle_PushWhenTheBranchCannotBeSaved_ShouldNotMarkTheDeliveryProcessed()
  {
    TrackedWithId("octo/repo");
    A.CallTo(() => _branches.FindAsync(RepositoryId, "main", A<CancellationToken>._)).Returns((Branch?)null);
    A.CallTo(() => _branches.AddAsync(A<Branch>._, A<CancellationToken>._))
     .Throws(new ConflictException("General.ConcurrentChange", "added in the meantime"));
    Stored("push", null, Push("refs/heads/main"));

    await Should.ThrowAsync<ConflictException>(async () => await HandleAsync());

    A.CallTo(() => _deliveries.MarkProcessedAsync(A<string>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // D. A failure of the forge leaves the delivery unprocessed as well: the branch is not where the push started, and
  // GitHub does not answer when it is asked where the branch is.
  [Fact]
  public async Task Handle_PushWhenTheForgeFails_ShouldNotMarkTheDeliveryProcessed()
  {
    TrackedWithId("octo/repo");
    A.CallTo(() => _branches.FindAsync(RepositoryId, "main", A<CancellationToken>._))
     .Returns(Branch.Create(RepositoryId, "main", GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3"), Now.AddDays(-1)));
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, "main", A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "GitHub does not answer."));
    Stored("push", null, Push("refs/heads/main"));

    await Should.ThrowAsync<BadGatewayException>(async () => await HandleAsync());

    A.CallTo(() => _deliveries.MarkProcessedAsync(A<string>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _branches.UpdateAsync(A<Branch>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("pull_request")]
  [InlineData("installation_target")]
  public async Task Handle_EventOfALaterFeature_ShouldOnlyMarkTheDeliveryProcessed(string @event)
  {
    Stored(@event, "opened", $$"""{"repository":{{RepositoryBody}}}""");

    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
    _queued.ShouldBeEmpty();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_SameDeliveryTwice_ShouldChangeNothingMore()
  {
    var repository = Tracked("octo/old-name");
    Stored("repository", "renamed", $$"""{"repository":{{RepositoryBody}}}""");

    await HandleAsync();
    var version = repository.Version;
    await HandleAsync();

    repository.Version.ShouldBe(version);
    A.CallTo(() => _repositories.UpdateAsync(repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static string Transferred(long ownerId, long previousOwnerId)
  {
    var repository = RepositoryBody.Replace("\"id\":1001", "\"id\":" + ownerId, StringComparison.Ordinal);

    return "{\"action\":\"transferred\",\"changes\":{\"owner\":{\"from\":{\"organization\":{\"id\":" + previousOwnerId + "}}}},\"repository\":" + repository + "}";
  }

  private static string Push(string reference, string commits = "[]") =>
    "{\"ref\":\"" + reference + "\",\"after\":\"" + PushedHead.Value + "\",\"deleted\":false,\"commits\":" + commits + "}";

  private static string InstallationBody(long installationId, long accountId, string login, string type = "Organization") =>
    "{\"installation\":{\"id\":" + installationId + ",\"account\":{\"id\":" + accountId + ",\"login\":\"" + login + "\",\"type\":\"" + type + "\"}}}";

  private void OnTheLaneOfTheInstance()
  {
    A.CallTo(() => _currentOrganization.IsKnown).Returns(false);
  }

  private async Task HandleAsync() => await _handler.Handle(new HandleWebhookDeliveryCommand("d-1"), Ct);

  private void Stored(string @event, string? action, string body)
  {
    A.CallTo(() => _deliveries.FindForProcessingAsync("d-1", A<CancellationToken>._))
     .Returns(new GitHubWebhookEvent("d-1", @event, action, Installation, 7, Encoding.UTF8.GetBytes(body)));
  }

  private TrackedRepository Tracked(string slug, bool isPrivate = false, string defaultBranch = "main")
  {
    var repository = TrackedRepository.Create(5, "7", RepositorySlug.Parse(slug), defaultBranch, isPrivate, "badge", Now.AddDays(-30));

    A.CallTo(() => _repositories.FindByExternalIdAsync("7", A<CancellationToken>._)).Returns(repository);

    return repository;
  }

  // A repository read from the database has its identifier, which a branch needs.
  private TrackedRepository TrackedWithId(string slug)
  {
    var repository = Tracked(slug);

    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, RepositoryId);

    return repository;
  }

  private void NotTracked()
  {
    A.CallTo(() => _repositories.FindByExternalIdAsync(A<string>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
  }

  private void ReservedLoginAudited(string target)
  {
    A.CallTo(
       () => _auditLog.RecordAsync(
         "Organization.Suspend",
         target,
         A<IReadOnlyDictionary<string, string>?>.That.Matches(d => d != null && d["Reason"] == "ReservedLogin"),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
  }

  private void Audited(string action, string tokens)
  {
    A.CallTo(
       () => _auditLog.RecordAsync(
         action,
         "0",
         A<IReadOnlyDictionary<string, string>?>.That.Matches(d => d != null && d["Tokens"] == tokens),
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly();
  }

  #endregion
}