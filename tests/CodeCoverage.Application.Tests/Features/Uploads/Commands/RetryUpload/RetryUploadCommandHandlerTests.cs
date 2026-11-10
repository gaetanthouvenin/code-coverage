// ------------------------------------------------------------------------------------------------
// <copyright file="RetryUploadCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Features.Uploads.Commands.RetryUpload;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Uploads.Commands.RetryUpload;

public sealed class RetryUploadCommandHandlerTests
{
  #region Fields

  private const long RepositoryId = 5;

  private const long CommitId = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IRawUploadStore _rawStore = A.Fake<IRawUploadStore>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly Upload _upload = Upload.Receive(CommitId, 1, "key", [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);

  #endregion

  #region Ctors

  public RetryUploadCommandHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, RepositoryId);

    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/app"), A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/other"), A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _commits.FindAsync(CommitId, A<CancellationToken>._)).Returns(CommitOf(RepositoryId));
    A.CallTo(() => _uploads.FindByPublicIdAsync(_upload.PublicId, A<CancellationToken>._)).Returns(_upload);
    A.CallTo(() => _rawStore.ListExpiredAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _reports.RequeueUploadsAsync(A<IReadOnlyList<UploadRequeue>>._, A<CancellationToken>._))
     .ReturnsLazily((IReadOnlyList<UploadRequeue> requeues, CancellationToken _) => [.. requeues.Select(static requeue => requeue.Upload)]);
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
  public async Task Handle_FailedUpload_ShouldRequeueItAndAudit()
  {
    Fail();

    await Handler().Handle(Command(), Ct);

    A.CallTo(() => _reports.RequeueUploadsAsync(
               A<IReadOnlyList<UploadRequeue>>.That.Matches(requeues => requeues.Count == 1
                                                                        && ReferenceEquals(requeues[0].Upload, _upload)
                                                                        && requeues[0].StartingLog.MessageTotal == 0),
               A<CancellationToken>._
             ))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(RetryUploadCommandHandler.AuditAction, _upload.PublicId.ToString("D"), null, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ProcessedUpload_ShouldThrowConflict()
  {
    _upload.MarkProcessed(1, 1, 0, default, ProcessingLog.Empty, Now);

    var exception = await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.NotFailed);
    A.CallTo(_reports).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  // Received, or held by a worker: its job is still to run, a second one would parse it twice.
  [Fact]
  public async Task Handle_UploadNotEnded_ShouldThrowConflict()
  {
    var exception = await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.NotFailed);
    A.CallTo(_reports).MustNotHaveHappened();
    A.CallTo(_rawStore).MustNotHaveHappened();
  }

  // The filter of the organization hides the upload of another one: the repository returns nothing.
  [Fact]
  public async Task Handle_UploadOfOtherOrganization_ShouldThrowNotFound()
  {
    A.CallTo(() => _uploads.FindByPublicIdAsync(_upload.PublicId, A<CancellationToken>._)).Returns((Upload?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.NotFound);
    A.CallTo(_commits).MustNotHaveHappened();
    A.CallTo(_reports).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  // The permission was checked on the repository of the address: the upload of another repository of the same
  // organization, which the caller may not administer, is not found either.
  [Fact]
  public async Task Handle_UploadOfAnotherRepository_ShouldThrowNotFound()
  {
    Fail();
    A.CallTo(() => _commits.FindAsync(CommitId, A<CancellationToken>._)).Returns(CommitOf(RepositoryId + 1));

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.NotFound);
    A.CallTo(_reports).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_CommitGone_ShouldThrowNotFound()
  {
    Fail();
    A.CallTo(() => _commits.FindAsync(CommitId, A<CancellationToken>._)).Returns((Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.NotFound);
  }

  [Theory]
  [InlineData("octo-org", "other")]
  [InlineData("not a login", "app")]
  public async Task Handle_UnknownRepository_ShouldThrowNotFound(string owner, string name)
  {
    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await Handler().Handle(new RetryUploadCommand(Forge.GitHub, owner, name, _upload.PublicId), Ct)
                    );

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    A.CallTo(_uploads).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RawExpired_ShouldThrowUnprocessable()
  {
    Fail();
    A.CallTo(() => _rawStore.ListExpiredAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns([_upload.PublicId]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.RawExpired);
    A.CallTo(_reports).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  // Another request took the upload between the read and the guarded update: its job is already queued.
  [Fact]
  public async Task Handle_TakenInTheMeantime_ShouldThrowConflictWithoutAudit()
  {
    Fail();
    A.CallTo(() => _reports.RequeueUploadsAsync(A<IReadOnlyList<UploadRequeue>>._, A<CancellationToken>._)).Returns([]);

    var exception = await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.NotFailed);
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  private static Commit CommitOf(long repositoryId)
  {
    var commit = Commit.Create(repositoryId, GitSha.Parse(new string('a', GitSha.Length)), Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, CommitId);

    return commit;
  }

  private void Fail() => _upload.MarkFailed(UploadErrors.ParseFailed, "Unreadable.", 1, 0, 0, ProcessingLog.Empty.Add("written by the processing"), Now);

  private RetryUploadCommand Command() => new(Forge.GitHub, "octo-org", "app", _upload.PublicId);

  private RetryUploadCommandHandler Handler() => new(_repositories, _commits, _uploads, new UploadRequeuer(_reports, _rawStore), _auditLog);

  #endregion
}
