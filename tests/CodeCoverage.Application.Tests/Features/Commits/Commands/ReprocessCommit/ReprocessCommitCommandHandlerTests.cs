// ------------------------------------------------------------------------------------------------
// <copyright file="ReprocessCommitCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Commands.ReprocessCommit;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Commits.Commands.ReprocessCommit;

public sealed class ReprocessCommitCommandHandlerTests
{
  #region Fields

  private const long CommitId = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IRawUploadStore _rawStore = A.Fake<IRawUploadStore>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly Upload _first = Upload.Receive(CommitId, 1, "first", [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);

  private readonly Upload _second = Upload.Receive(CommitId, 2, "second", [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);

  #endregion

  #region Ctors

  public ReprocessCommitCommandHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    var commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, CommitId);

    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/app"), A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/other"), A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _uploads.ListByCommitAsync(CommitId, A<CancellationToken>._)).Returns([_first, _second]);
    A.CallTo(() => _rawStore.ListExpiredAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns([]);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public void Command_ShouldRequireTheAdministrationOfTheRepository()
  {
    Command().RequiredPermission.ShouldBe(RepositoryPermission.Admin);
  }

  [Fact]
  public async Task Handle_ShouldQueueTheLockedJobAndAudit()
  {
    JobRequest? queued = null;
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).Invokes((JobRequest job, CancellationToken _) => queued = job).Returns(1L);

    await Handler().Handle(Command(), Ct);

    queued.ShouldNotBeNull();
    queued.Type.ShouldBe(JobTypes.ReprocessCommit);
    queued.PayloadJson.ShouldBe("""{"CommitId":9}""");
    queued.LockKey.ShouldBe("commit:9");
    queued.DedupKey.ShouldBe("reprocess:9");
    queued.Coalesce.ShouldBeTrue();

    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(ReprocessCommitCommandHandler.AuditAction, $"octo-org/app@{Sha.Value}", null, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();

    // Nothing is parsed nor put back in the queue in the request: the job does it under the lock of the commit.
    A.CallTo(_reports).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ShouldCheckTheRawFilesOfEveryUpload()
  {
    await Handler().Handle(Command(), Ct);

    A.CallTo(() => _rawStore.ListExpiredAsync(
               A<IReadOnlyCollection<Guid>>.That.IsSameSequenceAs(new[] { _first.PublicId, _second.PublicId }),
               A<CancellationToken>._
             ))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_RawExpired_ShouldThrowUnprocessable()
  {
    A.CallTo(() => _rawStore.ListExpiredAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns([_second.PublicId]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.RawExpired);
    A.CallTo(_jobQueue).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_CommitWithoutUpload_ShouldThrowUnprocessable()
  {
    A.CallTo(() => _uploads.ListByCommitAsync(CommitId, A<CancellationToken>._)).Returns([]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.NoUpload);
    A.CallTo(_jobQueue).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldThrowNotFound()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns((Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
    A.CallTo(_jobQueue).MustNotHaveHappened();
  }

  // The repository of another organization is not read by the filter: it is unknown here.
  [Theory]
  [InlineData("octo-org", "other")]
  [InlineData("not a login", "app")]
  public async Task Handle_UnknownRepository_ShouldThrowNotFound(string owner, string name)
  {
    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(new ReprocessCommitCommand(Forge.GitHub, owner, name, Sha), Ct));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    A.CallTo(_commits).MustNotHaveHappened();
    A.CallTo(_jobQueue).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_JobNotQueued_ShouldNotAudit()
  {
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).Throws(new InvalidOperationException("queue"));

    await Should.ThrowAsync<InvalidOperationException>(async () => await Handler().Handle(Command(), Ct));

    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  private static ReprocessCommitCommand Command() => new(Forge.GitHub, "octo-org", "app", Sha);

  private ReprocessCommitCommandHandler Handler() =>
    new(_repositories, _commits, _uploads, new UploadRequeuer(_reports, _rawStore), _jobQueue, _auditLog);

  #endregion
}
