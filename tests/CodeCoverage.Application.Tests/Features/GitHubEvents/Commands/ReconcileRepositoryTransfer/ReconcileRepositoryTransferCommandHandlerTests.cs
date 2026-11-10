// ------------------------------------------------------------------------------------------------
// <copyright file="ReconcileRepositoryTransferCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.GitHubEvents.Commands.ReconcileRepositoryTransfer;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.GitHubEvents.Commands.ReconcileRepositoryTransfer;

public sealed class ReconcileRepositoryTransferCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly ReconcileRepositoryTransferCommandHandler _handler;

  #endregion

  #region Ctors

  public ReconcileRepositoryTransferCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();
    var gateways = A.Fake<IForgeGatewayProvider>();
    var generator = A.Fake<ITokenGenerator>();
    var protector = A.Fake<ISecretProtector>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => generator.NewBadgeToken()).Returns("new-badge");
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => $"protected:{value}");
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "2002", "origin", OrganizationAccountType.Organization, Now));
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .Returns(new RepositoryDepartureTokens(3, 1));

    _handler = new ReconcileRepositoryTransferCommandHandler(
      _repositories,
      new RepositoryReconciler(_repositories, _directory, gateways, generator, protector, _auditLog, clock)
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_RepositoryGone_ShouldCloseItsRowAndAuditThreeActions()
  {
    var repository = Tracked();
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._)).Returns((ForgeRepositoryInfo?)null);

    await _handler.Handle(new ReconcileRepositoryTransferCommand("7"), Ct);

    repository.IsActive.ShouldBeFalse();
    repository.BadgeTokenProtected.ShouldBe("protected:new-badge");
    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappened(3, Times.Exactly);
    // Asked through the installation of the row, by the identifier of the repository on its forge.
    A.CallTo(() => _gateway.GetRepositoryAsync(new ForgeRepositoryRef(Forge.GitHub, "5", "7"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_AlreadyDeactivated_ShouldStillRevokeItsTokens()
  {
    // The removal from the installation was handled first: the row is inactive, its tokens are not revoked yet.
    var repository = Tracked();
    repository.Deactivate(Now.AddHours(-1));
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._)).Returns((ForgeRepositoryInfo?)null);

    await _handler.Handle(new ReconcileRepositoryTransferCommand("7"), Ct);

    A.CallTo(() => _repositories.SaveDepartedAsync(repository, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RepositoryStillOwnedByTheOrganization_ShouldKeepEverything()
  {
    var repository = Tracked();
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .Returns(new ForgeRepositoryInfo("7", "Origin", string.Empty, "repo", "main", false));

    await _handler.Handle(new ReconcileRepositoryTransferCommand("7"), Ct);

    repository.IsActive.ShouldBeTrue();
    repository.Version.ShouldBe(0);
    A.CallTo(() => _repositories.SaveDepartedAsync(A<TrackedRepository>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NeverTracked_ShouldDoNothing()
  {
    A.CallTo(() => _repositories.FindByExternalIdAsync("7", A<CancellationToken>._)).Returns((TrackedRepository?)null);

    await _handler.Handle(new ReconcileRepositoryTransferCommand("7"), Ct);

    A.CallTo(_gateway).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private TrackedRepository Tracked()
  {
    var repository = TrackedRepository.Create(5, "7", RepositorySlug.Parse("origin/repo"), "main", false, "badge", Now.AddDays(-30));

    A.CallTo(() => _repositories.FindByExternalIdAsync("7", A<CancellationToken>._)).Returns(repository);

    return repository;
  }

  #endregion
}