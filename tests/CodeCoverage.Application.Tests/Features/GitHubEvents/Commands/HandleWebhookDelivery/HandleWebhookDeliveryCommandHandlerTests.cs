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
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.HandleWebhookDelivery;

public sealed class HandleWebhookDeliveryCommandHandlerTests
{
  #region Fields

  private const long Installation = 42;

  private const string AccountId = "1001";

  private const string RepositoryBody = """{"id":7,"name":"New-Name","private":true,"default_branch":"trunk","owner":{"login":"octo","id":1001}}""";

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly IWebhookDeliveryRepository _deliveries = A.Fake<IWebhookDeliveryRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

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
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, AccountId, "octo", OrganizationAccountType.Organization, Now));
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

    _handler = new HandleWebhookDeliveryCommandHandler(_deliveries, _repositories, _directory, _currentOrganization, _jobQueue, reconciler, _clock);
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
    A.CallTo(() => _currentOrganization.IsKnown).Returns(false);
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
  public async Task Handle_InstallationGone_ShouldQueueNoListing(string action)
  {
    Stored("installation", action, "{}");

    await HandleAsync();

    _queued.ShouldBeEmpty();
    A.CallTo(() => _deliveries.MarkProcessedAsync("d-1", Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("installation", "created")]
  [InlineData("installation_repositories", "removed")]
  public async Task Handle_InstallationEventOnTheLaneOfTheInstance_ShouldQueueTheCoordinatorAndCreateNoRepository(string @event, string action)
  {
    A.CallTo(() => _currentOrganization.IsKnown).Returns(false);
    Stored(@event, action, """{"repositories_removed":[{"id":7}],"repositories_added":[{"id":8}]}""");

    await HandleAsync();

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.SyncInstallations);
    job.IsInstance.ShouldBeTrue();
    A.CallTo(_repositories).MustNotHaveHappened();
    A.CallTo(_directory).MustNotHaveHappened();
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

  [Theory]
  [InlineData("push")]
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

  private void NotTracked()
  {
    A.CallTo(() => _repositories.FindByExternalIdAsync(A<string>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
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