// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationRepositoriesCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallationRepositories;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.SyncInstallationRepositories;

public sealed class SyncInstallationRepositoriesCommandHandlerTests
{
  #region Fields

  private const long Installation = 42;

  private const long Organization = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly IGitHubAppGateway _gitHub = A.Fake<IGitHubAppGateway>();

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly List<TrackedRepository> _tracked = [];

  private readonly List<TrackedRepository> _added = [];

  private readonly SyncInstallationRepositoriesCommandHandler _handler;

  #endregion

  #region Ctors

  public SyncInstallationRepositoriesCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();
    var currentOrganization = A.Fake<ICurrentOrganization>();
    var generator = A.Fake<ITokenGenerator>();
    var protector = A.Fake<ISecretProtector>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => currentOrganization.IsKnown).Returns(true);
    A.CallTo(() => currentOrganization.Id).Returns(Organization);
    A.CallTo(() => generator.NewBadgeToken()).Returns("new-badge");
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => $"protected:{value}");
    A.CallTo(() => _installations.FindOwnerAsync(Installation, A<CancellationToken>._))
     .Returns(new InstallationOwner(Organization, OrganizationState.Active));
    A.CallTo(() => _repositories.ListAsync(A<CancellationToken>._)).ReturnsLazily(() => (IReadOnlyList<TrackedRepository>)[.. _tracked]);
    A.CallTo(() => _repositories.FindByExternalIdAsync(A<string>._, A<CancellationToken>._))
     .ReturnsLazily((string id, CancellationToken _) => _tracked.SingleOrDefault(r => r.ExternalId == id));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>._, A<CancellationToken>._))
     .Invokes((TrackedRepository repository, CancellationToken _) => _added.Add(repository));

    var reconciler = new RepositoryReconciler(
      _repositories,
      A.Fake<IOrganizationDirectory>(),
      A.Fake<IForgeGatewayProvider>(),
      generator,
      protector,
      _auditLog,
      clock
    );

    _handler = new SyncInstallationRepositoriesCommandHandler(_gitHub, _installations, _repositories, currentOrganization, reconciler, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldUpsertAndDeactivateMissing()
  {
    var renamed = Track("1", "octo/old-name");
    var unchanged = Track("2", "octo/same");
    var gone = Track("3", "octo/gone");
    Listed(Info("1", "new-name", isPrivate: true, defaultBranch: "trunk"), Info("2", "same"), Info("4", "fresh"));

    await HandleAsync();

    renamed.Name.ShouldBe("new-name");
    renamed.IsPrivate.ShouldBeTrue();
    renamed.DefaultBranch.ShouldBe("trunk");
    A.CallTo(() => _repositories.UpdateAsync(renamed, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    unchanged.Version.ShouldBe(0);
    A.CallTo(() => _repositories.UpdateAsync(unchanged, A<CancellationToken>._)).MustNotHaveHappened();

    gone.IsActive.ShouldBeFalse();
    A.CallTo(() => _repositories.UpdateAsync(gone, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    var fresh = _added.ShouldHaveSingleItem();
    fresh.ExternalId.ShouldBe("4");
    fresh.InstallationId.ShouldBe(Installation);
    fresh.Owner.ShouldBe("octo");
    fresh.Name.ShouldBe("fresh");
    fresh.IsActive.ShouldBeTrue();
    fresh.BadgeTokenProtected.ShouldBe("protected:new-badge");
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
    A.CallTo(() => _gitHub.ListInstallationRepositoriesAsync(Installation, A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "GitHub does not answer."));

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

    A.CallTo(_gitHub).MustNotHaveHappened();
    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static ForgeRepositoryInfo Info(string id, string name, bool isPrivate = false, string defaultBranch = "main") =>
    new(id, "octo", string.Empty, name, defaultBranch, isPrivate);

  private async Task HandleAsync() => await _handler.Handle(new SyncInstallationRepositoriesCommand(Installation), Ct);

  private void Listed(params ForgeRepositoryInfo[] repositories)
  {
    A.CallTo(() => _gitHub.ListInstallationRepositoriesAsync(Installation, A<CancellationToken>._)).Returns(repositories);
  }

  private TrackedRepository Track(string externalId, string slug, long installationId = Installation)
  {
    var repository = TrackedRepository.Create(installationId, externalId, RepositorySlug.Parse(slug), "main", false, "badge", Now.AddDays(-30));

    _tracked.Add(repository);

    return repository;
  }

  #endregion
}