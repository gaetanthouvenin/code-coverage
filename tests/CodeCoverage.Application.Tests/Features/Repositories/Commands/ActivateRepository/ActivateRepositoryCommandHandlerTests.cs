// ------------------------------------------------------------------------------------------------
// <copyright file="ActivateRepositoryCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Repositories.Commands.ActivateRepository;
using CodeCoverage.Application.Features.Repositories.Commands.DeactivateRepository;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Repositories.Commands.ActivateRepository;

// Both commands of M3.07: audited when they change the repository, a no-op otherwise.
public sealed class ActivateRepositoryCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly TrackedRepository _repository;

  #endregion

  #region Ctors

  public ActivateRepositoryCommandHandlerTests()
  {
    _repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_repository, 5L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(_repository);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_Disabled_ShouldEnableSaveAndAudit()
  {
    _repository.Disable(Now.AddDays(-1));

    await ActivateHandler().Handle(new ActivateRepositoryCommand(Forge.GitHub, "octo-org", "app"), Ct);

    _repository.DisabledAt.ShouldBeNull();
    A.CallTo(() => _repositories.UpdateAsync(_repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync("Repositories.Activate", "5", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // Decision 16: in the state already held, no write and no audit entry.
  [Fact]
  public async Task Handle_AlreadyEnabled_ShouldDoNothing()
  {
    await ActivateHandler().Handle(new ActivateRepositoryCommand(Forge.GitHub, "octo-org", "app"), Ct);

    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Deactivate_ShouldDisableSaveAndAudit()
  {
    await DeactivateHandler().Handle(new DeactivateRepositoryCommand(Forge.GitHub, "octo-org", "app"), Ct);

    _repository.DisabledAt.ShouldBe(Now);
    _repository.IsActive.ShouldBeTrue();
    A.CallTo(() => _repositories.UpdateAsync(_repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync("Repositories.Deactivate", "5", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_DeactivateAlreadyDisabled_ShouldDoNothing()
  {
    _repository.Disable(Now.AddDays(-1));

    await DeactivateHandler().Handle(new DeactivateRepositoryCommand(Forge.GitHub, "octo-org", "app"), Ct);

    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  private ActivateRepositoryCommandHandler ActivateHandler() => new(new CommitLocator(_repositories, _commits), _repositories, _auditLog, _clock);

  private DeactivateRepositoryCommandHandler DeactivateHandler() => new(new CommitLocator(_repositories, _commits), _repositories, _auditLog, _clock);

  #endregion
}
