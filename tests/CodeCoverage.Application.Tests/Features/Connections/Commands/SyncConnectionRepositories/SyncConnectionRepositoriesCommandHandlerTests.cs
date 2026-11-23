// ------------------------------------------------------------------------------------------------
// <copyright file="SyncConnectionRepositoriesCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Connections.Commands.SyncConnectionRepositories;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.SyncConnectionRepositories;

public sealed class SyncConnectionRepositoriesCommandHandlerTests
{
  #region Fields

  private const long Installation = 42;

  private const string ConnectionId = "contoso-devops";

  private const long OrganizationId = 9;

  private const string AccountId = "1001";

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly IForgeDirectoryGateway _gateway = A.Fake<IForgeDirectoryGateway>();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly List<TrackedRepository> _tracked = [];

  private readonly List<TrackedRepository> _added = [];

  private readonly List<JobRequest> _queued = [];

  private readonly SyncConnectionRepositoriesCommandHandler _handler;

  #endregion

  #region Ctors

  public SyncConnectionRepositoriesCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();
    var currentOrganization = A.Fake<ICurrentOrganization>();
    var generator = A.Fake<ITokenGenerator>();
    var protector = A.Fake<ISecretProtector>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => currentOrganization.IsKnown).Returns(true);
    A.CallTo(() => currentOrganization.Id).Returns(OrganizationId);
    A.CallTo(() => generator.NewBadgeToken()).Returns("new-badge");
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => $"protected:{value}");
    A.CallTo(() => _forges.GetDirectory(A<Forge>._)).Returns(_gateway);
    A.CallTo(() => _installations.FindOwnerAsync(Installation, A<CancellationToken>._))
     .Returns(new InstallationOwner(OrganizationId, OrganizationState.Active));
    A.CallTo(() => _connections.FindOwnerAsync(ConnectionId, A<CancellationToken>._))
     .Returns(new ForgeConnectionOwner(OrganizationId, Forge.AzureDevOps, OrganizationState.Active, ForgeConnectionStatus.Active, "protected-secret", null));
    A.CallTo(() => _gateway.GetAccountAsync(A<ForgeConnectionRef>._, A<CancellationToken>._)).Returns((ForgeAccountInfo?)null);
    A.CallTo(() => _repositories.ListAsync(A<CancellationToken>._)).ReturnsLazily(() => (IReadOnlyList<TrackedRepository>)[.. _tracked]);
    A.CallTo(() => _repositories.FindByExternalIdAsync(A<string>._, A<CancellationToken>._))
     .ReturnsLazily((string id, CancellationToken _) => _tracked.SingleOrDefault(r => r.ExternalId == id));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>._, A<CancellationToken>._))
     .Invokes((TrackedRepository repository, CancellationToken _) => _added.Add(repository));
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);

    var reconciler = new RepositoryReconciler(
      _repositories,
      A.Fake<IOrganizationDirectory>(),
      A.Fake<IForgeGatewayProvider>(),
      generator,
      protector,
      _auditLog,
      clock
    );

    _handler = new SyncConnectionRepositoriesCommandHandler(
      _forges,
      _installations,
      _connections,
      _repositories,
      _directory,
      currentOrganization,
      _jobQueue,
      reconciler,
      clock
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  private static ForgeConnectionRef GitHubConnection => new(Forge.GitHub, "42");

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_NewRepository_ShouldTrackIt()
  {
    Listed(Info("4", "fresh"));

    await HandleAsync();

    var fresh = _added.ShouldHaveSingleItem();
    fresh.ExternalId.ShouldBe("4");
    fresh.ConnectionId.ShouldBe("42");
    fresh.InstallationId.ShouldBe(Installation);
    fresh.Owner.ShouldBe("octo");
    fresh.Name.ShouldBe("fresh");
    fresh.IsActive.ShouldBeTrue();
    fresh.BadgeTokenProtected.ShouldBe("protected:new-badge");
    A.CallTo(() => _forges.GetDirectory(Forge.GitHub)).MustHaveHappened();
  }

  [Fact]
  public async Task Handle_MissingRepository_ShouldDeactivateIt()
  {
    var gone = Track("3", "octo/gone");
    Listed();

    await HandleAsync();

    gone.IsActive.ShouldBeFalse();
    A.CallTo(() => _repositories.UpdateAsync(gone, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_AccountRenamed_ShouldRenameTheOrganization()
  {
    Listed();
    var organization = Admitted("octo");
    var renamed = Admitted("octo-renamed");
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._)).Returns(organization);
    A.CallTo(() => _gateway.GetAccountAsync(GitHubConnection, A<CancellationToken>._)).Returns(new ForgeAccountInfo(AccountId, "octo-renamed"));
    A.CallTo(() => _directory.UpsertFromForgeAsync(Forge.GitHub, AccountId, "octo-renamed", OrganizationAccountType.Organization, A<CancellationToken>._))
     .Returns(renamed);

    await HandleAsync();

    A.CallTo(() => _directory.UpsertFromForgeAsync(Forge.GitHub, AccountId, "octo-renamed", OrganizationAccountType.Organization, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    _queued.ShouldHaveSingleItem().Type.ShouldBe(JobTypes.RewriteRepositoryOwner);
  }

  [Fact]
  public async Task Handle_SameLogin_ShouldNotRename()
  {
    Listed();
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._)).Returns(Admitted("octo"));
    A.CallTo(() => _gateway.GetAccountAsync(GitHubConnection, A<CancellationToken>._)).Returns(new ForgeAccountInfo(AccountId, "octo"));

    await HandleAsync();

    A.CallTo(() => _directory.UpsertFromForgeAsync(A<Forge>._, A<string>._, A<string>._, A<OrganizationAccountType>._, A<CancellationToken>._))
     .MustNotHaveHappened();
    _queued.ShouldBeEmpty();
  }

  // The organization is matched by the identifier of its account, never by a login.
  [Theory]
  [InlineData("2002", "other")]
  [InlineData(AccountId, "not a login")]
  [InlineData(AccountId, "")]
  public async Task Handle_AccountNotOfTheOrganizationOrLoginMalformed_ShouldNotRename(string accountId, string login)
  {
    Listed();
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._)).Returns(Admitted("octo"));
    A.CallTo(() => _gateway.GetAccountAsync(GitHubConnection, A<CancellationToken>._)).Returns(new ForgeAccountInfo(accountId, login));

    await HandleAsync();

    A.CallTo(() => _directory.UpsertFromForgeAsync(A<Forge>._, A<string>._, A<string>._, A<OrganizationAccountType>._, A<CancellationToken>._))
     .MustNotHaveHappened();
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_UnchangedRepository_ShouldNotBeSaved()
  {
    var unchanged = Track("2", "octo/same");
    Listed(Info("2", "same"));

    await HandleAsync();

    unchanged.Version.ShouldBe(0);
    A.CallTo(() => _repositories.UpdateAsync(unchanged, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RenamedRepository_ShouldBeUpdated()
  {
    var renamed = Track("1", "octo/old-name");
    Listed(Info("1", "new-name", isPrivate: true, defaultBranch: "trunk"));

    await HandleAsync();

    renamed.Name.ShouldBe("new-name");
    renamed.IsPrivate.ShouldBeTrue();
    renamed.DefaultBranch.ShouldBe("trunk");
    A.CallTo(() => _repositories.UpdateAsync(renamed, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_MissingRepository_ShouldKeepItsTokens()
  {
    Track("3", "octo/gone");
    Listed();

    await HandleAsync();

    // Missing from the list is not a proven transfer: nothing is revoked, replaced or audited.
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_InactiveRepositoryServedAgain_ShouldBeActivatedUnderThisInstallation()
  {
    var back = Track("1", "octo/back", installationId: 5);
    back.Deactivate(Now.AddDays(-1));
    Listed(Info("1", "back"));

    await HandleAsync();

    back.IsActive.ShouldBeTrue();
    back.InstallationId.ShouldBe(Installation);
    back.ConnectionId.ShouldBe("42");
    A.CallTo(() => _repositories.UpdateAsync(back, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_AlreadyInactiveAndStillMissing_ShouldNotBeSavedAgain()
  {
    var gone = Track("3", "octo/gone");
    gone.Deactivate(Now.AddDays(-1));
    Listed();

    await HandleAsync();

    gone.Version.ShouldBe(1);
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RepositoryAddedInTheMeantime_ShouldGoOnWithTheOthers()
  {
    Listed(Info("4", "taken"), Info("5", "fresh"));
    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>.That.Matches(r => r.ExternalId == "4"), A<CancellationToken>._))
     .Throws(new ConflictException(GeneralErrors.ConcurrentChange, "added in the meantime"));

    await HandleAsync();

    _added.ShouldHaveSingleItem().ExternalId.ShouldBe("5");
  }

  // One name still held by a tracked row must not stop the others, nor the deactivations.
  [Fact]
  public async Task Handle_RenameRefused_ShouldGoOnWithTheOthersAndTheDeactivations()
  {
    var renamed = Track("1", "octo/old-name");
    var gone = Track("3", "octo/gone");
    Listed(Info("1", "taken"), Info("5", "fresh"));
    A.CallTo(() => _repositories.UpdateAsync(renamed, A<CancellationToken>._))
     .Throws(new ConflictException(GeneralErrors.ConcurrentChange, "name held"));

    await HandleAsync();

    _added.ShouldHaveSingleItem().ExternalId.ShouldBe("5");
    gone.IsActive.ShouldBeFalse();
    A.CallTo(() => _repositories.UpdateAsync(gone, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NameHeldByAnInactiveRow_ShouldReleaseItAndTrackTheNewRepository()
  {
    var deleted = Track("1", "octo/app");
    deleted.Deactivate(Now.AddDays(-1));
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo/app"), A<CancellationToken>._)).Returns(deleted);
    Listed(Info("2", "app"));

    await HandleAsync();

    deleted.Name.ShouldBe("app~1");
    deleted.IsActive.ShouldBeFalse();
    _added.ShouldHaveSingleItem().ExternalId.ShouldBe("2");
  }

  [Fact]
  public async Task Handle_ListingFails_ShouldDeactivateNothing()
  {
    var repository = Track("1", "octo/repo");
    A.CallTo(() => _gateway.ListRepositoriesAsync(GitHubConnection, A<CancellationToken>._))
     .Throws(new BadGatewayException("Forge.Unavailable", "GitHub does not answer."));

    await Should.ThrowAsync<BadGatewayException>(async () => await HandleAsync());

    repository.IsActive.ShouldBeTrue();
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(null)]
  [InlineData(10L)]
  public async Task Handle_InstallationOfAnotherOrganization_ShouldListNothing(long? ownerOrganizationId)
  {
    Track("1", "octo/repo");
    A.CallTo(() => _installations.FindOwnerAsync(Installation, A<CancellationToken>._))
     .Returns(ownerOrganizationId is { } id ? new InstallationOwner(id, OrganizationState.Active) : null);

    await HandleAsync();

    A.CallTo(_gateway).MustNotHaveHappened();
    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_AzureDevOpsConnection_ShouldTrackUnderTheConnectionAlone()
  {
    var connection = new ForgeConnectionRef(Forge.AzureDevOps, ConnectionId);
    var back = TrackOnConnection("b-1", RepositorySlug.Create("contoso", "project", "back"));
    back.Deactivate(Now.AddDays(-1));
    A.CallTo(() => _gateway.ListRepositoriesAsync(connection, A<CancellationToken>._))
     .Returns([Info("a-1", "fresh", "contoso", "project"), Info("b-1", "back", "contoso", "project")]);

    await HandleAsync(new SyncConnectionRepositoriesCommand(Forge.AzureDevOps, ConnectionId));

    var fresh = _added.ShouldHaveSingleItem();
    fresh.ConnectionId.ShouldBe(ConnectionId);
    fresh.InstallationId.ShouldBeNull();
    fresh.Namespace.ShouldBe("project");
    back.IsActive.ShouldBeTrue();
    back.InstallationId.ShouldBeNull();
    A.CallTo(() => _forges.GetDirectory(Forge.AzureDevOps)).MustHaveHappened();
    A.CallTo(_installations).MustNotHaveHappened();
    _queued.ShouldHaveSingleItem().Type.ShouldBe(JobTypes.EnsureForgeHooks);
  }

  [Fact]
  public async Task Handle_NewProject_ShouldQueueEnsureForgeHooks()
  {
    var connection = new ForgeConnectionRef(Forge.AzureDevOps, ConnectionId);
    TrackOnConnection("a-1", RepositorySlug.Create("contoso", "platform", "api"));
    A.CallTo(() => _gateway.ListRepositoriesAsync(connection, A<CancellationToken>._))
     .Returns([Info("a-1", "api", "contoso", "platform"), Info("t-1", "cli", "contoso", "tools")]);

    await HandleAsync(new SyncConnectionRepositoriesCommand(Forge.AzureDevOps, ConnectionId));

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.EnsureForgeHooks);
    job.PayloadJson.ShouldBe(ForgeHookJobs.EnsurePayload(ConnectionId));
    job.DedupKey.ShouldBe(ForgeHookJobs.EnsureKey(ConnectionId));
    job.Coalesce.ShouldBeTrue();
    job.OrganizationId.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_NoRepositoryAppeared_ShouldNotQueueEnsureForgeHooks()
  {
    var connection = new ForgeConnectionRef(Forge.AzureDevOps, ConnectionId);
    TrackOnConnection("a-1", RepositorySlug.Create("contoso", "platform", "api"));
    TrackOnConnection("a-2", RepositorySlug.Create("contoso", "platform", "web"));
    A.CallTo(() => _gateway.ListRepositoriesAsync(connection, A<CancellationToken>._))
     .Returns([Info("a-1", "api", "contoso", "platform"), Info("a-2", "web-renamed", "contoso", "platform")]);

    await HandleAsync(new SyncConnectionRepositoriesCommand(Forge.AzureDevOps, ConnectionId));

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NewRepositoryAddedInTheMeantime_ShouldNotQueueEnsureForgeHooks()
  {
    var connection = new ForgeConnectionRef(Forge.AzureDevOps, ConnectionId);
    A.CallTo(() => _gateway.ListRepositoriesAsync(connection, A<CancellationToken>._)).Returns([Info("t-1", "cli", "contoso", "tools")]);
    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>._, A<CancellationToken>._))
     .Throws(new ConflictException(GeneralErrors.ConcurrentChange, "added in the meantime"));

    await HandleAsync(new SyncConnectionRepositoriesCommand(Forge.AzureDevOps, ConnectionId));

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NewGitHubRepository_ShouldNotQueueEnsureForgeHooks()
  {
    Listed(Info("4", "fresh"));

    await HandleAsync();

    _added.ShouldHaveSingleItem();
    _queued.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(OrganizationId, Forge.AzureDevOps, ForgeConnectionStatus.Suspended)]
  [InlineData(OrganizationId, Forge.GitLab, ForgeConnectionStatus.Active)]
  [InlineData(10L, Forge.AzureDevOps, ForgeConnectionStatus.Active)]
  public async Task Handle_ConnectionNotServingTheScope_ShouldListNothing(long ownerOrganizationId, Forge forge, ForgeConnectionStatus status)
  {
    A.CallTo(() => _connections.FindOwnerAsync(ConnectionId, A<CancellationToken>._))
     .Returns(new ForgeConnectionOwner(ownerOrganizationId, forge, OrganizationState.Active, status, "protected-secret", null));

    await HandleAsync(new SyncConnectionRepositoriesCommand(Forge.AzureDevOps, ConnectionId));

    A.CallTo(_gateway).MustNotHaveHappened();
    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownConnection_ShouldListNothing()
  {
    A.CallTo(() => _connections.FindOwnerAsync(ConnectionId, A<CancellationToken>._)).Returns((ForgeConnectionOwner?)null);

    await HandleAsync(new SyncConnectionRepositoriesCommand(Forge.AzureDevOps, ConnectionId));

    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static ForgeRepositoryInfo Info(string id, string name, bool isPrivate = false, string defaultBranch = "main") =>
    new(id, "octo", string.Empty, name, defaultBranch, isPrivate);

  private static ForgeRepositoryInfo Info(string id, string name, string owner, string @namespace) =>
    new(id, owner, @namespace, name, "main", IsPrivate: true);

  private static Organization Admitted(string login)
  {
    var organization = Organization.CreatePending(Forge.GitHub, AccountId, login, OrganizationAccountType.Organization, Now.AddDays(-30));

    organization.Admit("admin", Now.AddDays(-29));

    return organization;
  }

  private async Task HandleAsync() => await HandleAsync(new SyncConnectionRepositoriesCommand(Forge.GitHub, "42"));

  private async Task HandleAsync(SyncConnectionRepositoriesCommand command) => await _handler.Handle(command, Ct);

  private void Listed(params ForgeRepositoryInfo[] repositories)
  {
    A.CallTo(() => _gateway.ListRepositoriesAsync(GitHubConnection, A<CancellationToken>._)).Returns(repositories);
  }

  private TrackedRepository Track(string externalId, string slug, long installationId = Installation)
  {
    var repository = TrackedRepository.Create(installationId, externalId, RepositorySlug.Parse(slug), "main", false, "badge", Now.AddDays(-30));

    _tracked.Add(repository);

    return repository;
  }

  private TrackedRepository TrackOnConnection(string externalId, RepositorySlug slug)
  {
    var repository = TrackedRepository.Create(ConnectionId, null, externalId, slug, "main", true, "badge", Now.AddDays(-30));

    _tracked.Add(repository);

    return repository;
  }

  #endregion
}
