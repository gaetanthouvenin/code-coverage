// ------------------------------------------------------------------------------------------------
// <copyright file="TriggerNotifyCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Notifications.Commands.TriggerNotify;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Notifications.Commands.TriggerNotify;

// The handler only: the command is repository scoped, and no sender but the system passes the pipeline before M3.04.
public sealed class TriggerNotifyCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly List<JobRequest> _queued = [];

  private readonly TriggerNotifyCommandHandler _handler;

  #endregion

  #region Ctors

  public TriggerNotifyCommandHandlerTests()
  {
    var repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/repo"), "main", false, "badge", Now);
    var commit = Commit.Create(5, Sha, Now);

    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo/repo"), A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);

    _handler = new TriggerNotifyCommandHandler(_repositories, _commits, _jobQueue, _auditLog);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldQueueTheForcedNotificationOfTheCommitAndAuditIt()
  {
    await _handler.Handle(new TriggerNotifyCommand(Forge.GitHub, "octo", "repo", Sha), Ct);

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":true}""");
    job.LockKey.ShouldBe("commit:9");
    job.DedupKey.ShouldBe("notify:9:forced");
    job.Coalesce.ShouldBeTrue();
    // The organization of the scope: the job names none.
    job.OrganizationId.ShouldBeNull();
    A.CallTo(() => _auditLog.RecordAsync("Commits.Notify", $"octo/repo@{Sha.Value}", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Command_ShouldBeReservedToTheAdministratorsOfTheRepository()
  {
    new TriggerNotifyCommand(Forge.GitHub, "octo", "repo", Sha).RequiredPermission.ShouldBe(RepositoryPermission.Admin);
  }

  [Theory]
  [InlineData("octo", "other")]
  [InlineData("not an owner", "repo")]
  public async Task Handle_UnknownRepository_ShouldThrowNotFoundAndQueueNothing(string owner, string name)
  {
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo/other"), A<CancellationToken>._)).Returns((TrackedRepository?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new TriggerNotifyCommand(Forge.GitHub, owner, name, Sha), Ct));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    _queued.ShouldBeEmpty();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldThrowNotFoundAndQueueNothing()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns((Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new TriggerNotifyCommand(Forge.GitHub, "octo", "repo", Sha), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  #endregion
}
