// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryReconcilerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Services;

public sealed class RepositoryReconcilerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly ITokenGenerator _generator = A.Fake<ITokenGenerator>();

  private readonly RepositoryReconciler _reconciler;

  #endregion

  #region Ctors

  public RepositoryReconcilerTests()
  {
    var clock = A.Fake<IClock>();
    var gateways = A.Fake<IForgeGatewayProvider>();
    var protector = A.Fake<ISecretProtector>();
    var directory = A.Fake<IOrganizationDirectory>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _generator.NewBadgeToken()).ReturnsNextFromSequence("badge-1", "badge-2");
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => $"protected:{value}");
    A.CallTo(() => directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "1001", "octo", OrganizationAccountType.Organization, Now));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._)).Returns((ForgeRepositoryInfo?)null);

    _reconciler = new RepositoryReconciler(_repositories, directory, gateways, _generator, protector, _auditLog, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A repository deleted, then created again under the same name: the row that is no longer tracked gives the name up.
  [Fact]
  public async Task Save_NameHeldByAnInactiveRow_ShouldReleaseItFirst()
  {
    var holder = Tracked("1", "octo/app");
    holder.Deactivate(Now.AddDays(-1));
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo/app"), A<CancellationToken>._)).Returns(holder);
    var created = Tracked("2", "octo/app");

    await _reconciler.SaveAsync(created, isNew: true, job: null, Ct);

    holder.Name.ShouldBe("app~1");
    A.CallTo(() => _repositories.UpdateAsync(holder, A<CancellationToken>._)).MustHaveHappenedOnceExactly()
     .Then(A.CallTo(() => _repositories.AddAsync(created, A<CancellationToken>._)).MustHaveHappenedOnceExactly());
  }

  [Fact]
  public async Task Save_NameHeldByATrackedRow_ShouldLeaveItToTheForge()
  {
    var holder = Tracked("1", "octo/app");
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(holder);
    var renamed = Tracked("2", "octo/app");

    await _reconciler.SaveAsync(renamed, isNew: false, job: null, Ct);

    holder.Name.ShouldBe("app");
    A.CallTo(() => _repositories.UpdateAsync(holder, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repositories.UpdateAsync(renamed, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Save_OwnInactiveRow_ShouldKeepItsName()
  {
    var repository = Tracked("1", "octo/app");
    repository.Deactivate(Now);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);

    await _reconciler.SaveAsync(repository, isNew: false, job: null, Ct);

    repository.Name.ShouldBe("app");
    A.CallTo(() => _repositories.UpdateAsync(repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Save_WithAJob_ShouldQueueItInTheTransactionOfTheSave(bool isNew)
  {
    var repository = Tracked("1", "octo/app");
    var job = SyncInstallationsJobs.Create();

    await _reconciler.SaveAsync(repository, isNew, job, Ct);

    A.CallTo(() => _repositories.SaveWithJobAsync(repository, job, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task CloseTransferred_Twice_ShouldReplaceAndAuditOnce()
  {
    var repository = Tracked("1", "octo/app");
    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._))
     .ReturnsNextFromSequence(new RepositoryDepartureTokens(2, 1), new RepositoryDepartureTokens(0, 0));

    (await _reconciler.CloseTransferredAsync(repository, Ct)).ShouldBeTrue();
    var version = repository.Version;
    (await _reconciler.CloseTransferredAsync(repository, Ct)).ShouldBeTrue();

    // The second request, a retry or the other side of the transfer, finds the row closed.
    repository.Version.ShouldBe(version);
    repository.BadgeTokenProtected.ShouldBe("protected:badge-1");
    A.CallTo(() => _generator.NewBadgeToken()).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappened(3, Times.Exactly);
  }

  // An installation that no longer lists the repository only deactivates its row, and touches no token: inactive
  // does not mean closed. The transfer that comes after still closes the row, with or without a token to revoke.
  [Theory]
  [InlineData(2)]
  [InlineData(0)]
  public async Task CloseTransferred_RowOnlyDeactivated_ShouldStillCloseIt(int revoked)
  {
    var repository = Tracked("1", "octo/app");
    repository.Deactivate(Now.AddHours(-1));
    var version = repository.Version;
    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._)).Returns(new RepositoryDepartureTokens(revoked, 1));

    (await _reconciler.CloseTransferredAsync(repository, Ct)).ShouldBeTrue();

    repository.IsActive.ShouldBeFalse();
    repository.TransferredAt.ShouldBe(Now);
    repository.BadgeTokenProtected.ShouldBe("protected:badge-1");
    // Changed, so that the save knows it closes something and marks the tokens of the organization.
    repository.Version.ShouldBeGreaterThan(version);
    A.CallTo(() => _auditLog.RecordAsync(RepositoryReconciler.RevokeTokensAuditAction, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappened(revoked > 0 ? 1 : 0, Times.Exactly);
    A.CallTo(() => _auditLog.RecordAsync(RepositoryReconciler.RotateBadgeTokenAuditAction, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(RepositoryReconciler.AdviseRotationAuditAction, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A repository that came back and leaves again is closed again: its row was opened by its return.
  [Fact]
  public async Task CloseTransferred_RepositoryThatCameBackAndLeavesAgain_ShouldCloseItAgain()
  {
    var repository = Tracked("1", "octo/app");
    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._)).Returns(new RepositoryDepartureTokens(0, 1));

    await _reconciler.CloseTransferredAsync(repository, Ct);
    repository.Activate(7, Now);
    repository.TransferredAt.ShouldBeNull();
    await _reconciler.CloseTransferredAsync(repository, Ct);

    repository.IsActive.ShouldBeFalse();
    repository.TransferredAt.ShouldBe(Now);
    repository.BadgeTokenProtected.ShouldBe("protected:badge-2");
    A.CallTo(() => _auditLog.RecordAsync(RepositoryReconciler.RotateBadgeTokenAuditAction, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappenedTwiceExactly();
  }

  // The account is known by its identifier: a login can be renamed, or taken again by another account.
  [Theory]
  [InlineData("renamed-org", "1001", false)]
  [InlineData("octo", "9009", true)]
  [InlineData("OCTO", null, false)]
  [InlineData("someone-else", null, true)]
  public async Task CloseTransferred_ShouldDecideTheOwnerByItsAccountIdentifier(string login, string? accountId, bool closed)
  {
    var repository = Tracked("1", "octo/app");
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .Returns(new ForgeRepositoryInfo("1", login, string.Empty, "app", "main", false, accountId));
    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._)).Returns(new RepositoryDepartureTokens(0, 0));

    (await _reconciler.CloseTransferredAsync(repository, Ct)).ShouldBe(closed);

    repository.IsActive.ShouldBe(!closed);
  }

  private static TrackedRepository Tracked(string externalId, string slug) =>
    TrackedRepository.Create(5, externalId, RepositorySlug.Parse(slug), "main", false, "badge", Now.AddDays(-30));

  #endregion
}