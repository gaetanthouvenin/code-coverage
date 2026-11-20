// ------------------------------------------------------------------------------------------------
// <copyright file="HandleForgeDeliveryCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Webhooks;
using CodeCoverage.Application.Features.Webhooks.Commands.HandleForgeDelivery;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Webhooks.Commands.HandleForgeDelivery;

public sealed class HandleForgeDeliveryCommandHandlerTests
{
  #region Fields

  private const string DeliveryId = "d-1";

  private const string ConnectionId = "conn-1";

  private const string AccountId = "acc-1";

  private const long RepositoryId = 5;

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Started = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha PushedHead = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private readonly IWebhookDeliveryRepository _deliveries = A.Fake<IWebhookDeliveryRepository>();

  private readonly IForgeEventTranslator _translator = A.Fake<IForgeEventTranslator>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly ICurrentOrganization _currentOrganization = A.Fake<ICurrentOrganization>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  private readonly List<JobRequest> _queued = [];

  private readonly ForgeDelivery _delivery = new(Forge.AzureDevOps, DeliveryId, "git.push", null, ConnectionId, "7", "{}"u8.ToArray());

  private readonly HandleForgeDeliveryCommandHandler _handler;

  #endregion

  #region Ctors

  public HandleForgeDeliveryCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _currentOrganization.IsKnown).Returns(true);
    A.CallTo(() => _currentOrganization.Id).Returns(9);
    _directory.Current = AdmittedOrganization(Forge.AzureDevOps);
    A.CallTo(() => _forges.GetEventTranslator(Forge.AzureDevOps)).Returns(_translator);
    A.CallTo(() => _deliveries.FindForgeDeliveryForProcessingAsync(Forge.AzureDevOps, DeliveryId, A<CancellationToken>._)).Returns(_delivery);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);

    _handler = new HandleForgeDeliveryCommandHandler(
      _deliveries,
      _forges,
      _repositories,
      _directory,
      _currentOrganization,
      _jobQueue,
      new PushProcessor(
        _branches,
        _commits,
        _repositories,
        new BranchHeadTracker(_commits, new CommitAncestry(A.Fake<IGitCommitRepository>(), _forges, clock)),
        new ConfigResolver(A.Fake<ICommitConfigRepository>(), _forges, A.Fake<ICoverageConfigParser>(), A.Fake<IOrganizationSettingsProvider>(), clock),
        _forges,
        clock
      ),
      clock
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_NullCommand_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  [Fact]
  public async Task Handle_UnknownDelivery_ShouldThrowNotFound()
  {
    A.CallTo(() => _deliveries.FindForgeDeliveryForProcessingAsync(Forge.AzureDevOps, DeliveryId, A<CancellationToken>._)).Returns((ForgeDelivery?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await HandleAsync());

    exception.ErrorCode.ShouldBe(WebhookErrors.DeliveryNotFound);
    A.CallTo(_translator).MustNotHaveHappened();
    NotMarked();
  }

  [Fact]
  public async Task Handle_Push_ShouldProcessTheBranch()
  {
    var branch = Branch.Create(RepositoryId, "main", Started, Now.AddMinutes(-1));
    Tracked();
    A.CallTo(() => _branches.FindAsync(RepositoryId, "main", A<CancellationToken>._)).Returns(branch);
    Translated(new ForgePushEvent("7", "main", PushedHead, Deleted: false, TouchesConfig: false, Started));

    await HandleAsync();

    branch.HeadSha.ShouldBe(PushedHead);
    A.CallTo(() => _branches.UpdateAsync(branch, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    Marked();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_PushOfARepositoryNotTracked_ShouldChangeNoBranch(bool inactive)
  {
    NotTracked(inactive);
    Translated(new ForgePushEvent("7", "main", PushedHead, Deleted: false, TouchesConfig: false, Started));

    await HandleAsync();

    A.CallTo(_branches).MustNotHaveHappened();
    Marked();
  }

  [Fact]
  public async Task Handle_PullRequest_ShouldQueueItsSync()
  {
    Tracked();
    Translated(new ForgePullRequestEvent("7", 12));

    await HandleAsync();

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.SyncPullRequest);
    job.PayloadJson.ShouldBe("""{"RepositoryId":5,"Number":12}""");
    Marked();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_PullRequestOfARepositoryNotTracked_ShouldQueueNothing(bool inactive)
  {
    NotTracked(inactive);
    Translated(new ForgePullRequestEvent("7", 12));

    await HandleAsync();

    _queued.ShouldBeEmpty();
    Marked();
  }

  [Theory]
  [InlineData(ForgeRepositoryEventKind.Created)]
  [InlineData(ForgeRepositoryEventKind.Renamed)]
  [InlineData(ForgeRepositoryEventKind.Deleted)]
  public async Task Handle_RepositoryRenamed_ShouldQueueReconciliation(ForgeRepositoryEventKind kind)
  {
    Translated(new ForgeRepositoryEvent(kind, Info("someone-else")));

    await HandleAsync();

    ShouldQueueTheReconciliation();
    Marked();
  }

  [Fact]
  public async Task Handle_RepositoryTransferredToTheOrganization_ShouldQueueReconciliation()
  {
    // The identifier of the account is compared in its normalized form.
    Translated(new ForgeRepositoryEvent(ForgeRepositoryEventKind.Transferred, Info("ACC-1"), "former"));

    await HandleAsync();

    ShouldQueueTheReconciliation();
  }

  [Fact]
  public async Task Handle_RepositoryTransferredWithoutItsAccount_ShouldQueueReconciliation()
  {
    Translated(new ForgeRepositoryEvent(ForgeRepositoryEventKind.Transferred, Info(null)));

    await HandleAsync();

    ShouldQueueTheReconciliation();
  }

  [Fact]
  public async Task Handle_RepositoryTransferredAway_ShouldQueueTheClosingOfItsRow()
  {
    Translated(new ForgeRepositoryEvent(ForgeRepositoryEventKind.Transferred, Info("acc-2")));

    await HandleAsync();

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.ReconcileRepositoryTransfer);
    job.OrganizationId.ShouldBeNull();
    job.IsInstance.ShouldBeFalse();
    job.PayloadJson.ShouldBe(RepositoryReconciliationJobs.TransferPayload(Forge.AzureDevOps, "7"));
    job.LockKey.ShouldBe("repo:7");
    job.DedupKey.ShouldBe("transfer:7");
    job.Coalesce.ShouldBeTrue();
    Marked();
  }

  [Fact]
  public async Task Handle_Ignored_ShouldMarkTheDeliveryIgnored()
  {
    Translated(null);

    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
    _queued.ShouldBeEmpty();
    Marked();
  }

  [Fact]
  public async Task Handle_EventNotKnown_ShouldMarkTheDeliveryProcessed()
  {
    Translated(A.Fake<ForgeEvent>());

    await HandleAsync();

    A.CallTo(_repositories).MustNotHaveHappened();
    _queued.ShouldBeEmpty();
    Marked();
  }

  [Fact]
  public async Task Handle_OutOfAnOrganization_ShouldTranslateNothing()
  {
    A.CallTo(() => _currentOrganization.IsKnown).Returns(false);
    _directory.Current = null;

    await HandleAsync();

    A.CallTo(_forges).MustNotHaveHappened();
    Marked();
  }

  [Fact]
  public async Task Handle_OrganizationNoLongerStored_ShouldTranslateNothing()
  {
    _directory.Current = null;

    await HandleAsync();

    A.CallTo(_forges).MustNotHaveHappened();
    Marked();
  }

  [Fact]
  public async Task Handle_OrganizationOfAnotherForge_ShouldTranslateNothing()
  {
    _directory.Current = AdmittedOrganization(Forge.GitHub);

    await HandleAsync();

    A.CallTo(_forges).MustNotHaveHappened();
    Marked();
  }

  private static ForgeRepositoryInfo Info(string? ownerExternalId) =>
    new("7", "octo", string.Empty, "repo", "main", IsPrivate: false, ownerExternalId);

  private Organization AdmittedOrganization(Forge forge)
  {
    var organization = Organization.CreatePending(forge, AccountId, "octo", OrganizationAccountType.Organization, Now);
    organization.Admit("tests", Now);

    return _directory.Add(organization);
  }

  private void Translated(ForgeEvent? forgeEvent)
  {
    A.CallTo(() => _translator.Translate(_delivery)).Returns(forgeEvent);
  }

  // A repository read from the database has its identifier, which a branch and a synchronization need.
  private void Tracked()
  {
    var repository = TrackedRepository.Create(ConnectionId, null, "7", RepositorySlug.Parse("octo/repo"), "main", false, "badge", Now.AddDays(-30));

    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, RepositoryId);
    A.CallTo(() => _repositories.FindByExternalIdAsync("7", A<CancellationToken>._)).Returns(repository);
  }

  private void NotTracked(bool inactive)
  {
    TrackedRepository? repository = null;

    if (inactive)
    {
      repository = TrackedRepository.Create(ConnectionId, null, "7", RepositorySlug.Parse("octo/repo"), "main", false, "badge", Now.AddDays(-30));
      repository.Deactivate(Now.AddDays(-1));
    }

    A.CallTo(() => _repositories.FindByExternalIdAsync("7", A<CancellationToken>._)).Returns(repository);
  }

  private void ShouldQueueTheReconciliation()
  {
    var job = _queued.ShouldHaveSingleItem();
    var expected = RepositoryReconciliationJobs.ForConnection(Forge.AzureDevOps, ConnectionId);

    job.Type.ShouldBe(JobTypes.SyncConnectionRepositories);
    job.PayloadJson.ShouldBe(expected.PayloadJson);
    job.LockKey.ShouldBe(expected.LockKey);
    job.DedupKey.ShouldBe(expected.DedupKey);
  }

  private void Marked()
  {
    A.CallTo(() => _deliveries.MarkForgeDeliveryProcessedAsync(Forge.AzureDevOps, DeliveryId, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  private void NotMarked()
  {
    A.CallTo(() => _deliveries.MarkForgeDeliveryProcessedAsync(A<Forge>._, A<string>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private async Task HandleAsync() => await _handler.Handle(new HandleForgeDeliveryCommand(Forge.AzureDevOps, DeliveryId), Ct);

  #endregion
}
