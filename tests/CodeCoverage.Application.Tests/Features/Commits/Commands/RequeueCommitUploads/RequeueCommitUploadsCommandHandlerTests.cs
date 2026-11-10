// ------------------------------------------------------------------------------------------------
// <copyright file="RequeueCommitUploadsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Commands.RequeueCommitUploads;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Security;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Tests.Features.Commits.Commands.RequeueCommitUploads;

// The work of the ReprocessCommit job, which holds the lock of the commit.
public sealed class RequeueCommitUploadsCommandHandlerTests
{
  #region Fields

  private const long CommitId = 9;

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IRawUploadStore _rawStore = A.Fake<IRawUploadStore>();

  private IReadOnlyList<UploadRequeue>? _requeued;

  #endregion

  #region Ctors

  public RequeueCommitUploadsCommandHandlerTests()
  {
    var commit = Commit.Create(5, GitSha.Parse(new string('a', GitSha.Length)), Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, CommitId);

    A.CallTo(() => _commits.FindAsync(CommitId, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _rawStore.ListExpiredAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _reports.RequeueUploadsAsync(A<IReadOnlyList<UploadRequeue>>._, A<CancellationToken>._))
     .ReturnsLazily((IReadOnlyList<UploadRequeue> requeues, CancellationToken _) =>
       {
         _requeued = requeues;

         return [.. requeues.Select(static requeue => requeue.Upload)];
       });
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public void Command_ShouldBeSentByTheSystemOnly()
  {
    new RequeueCommitUploadsCommand(CommitId).ShouldBeAssignableTo<ISystemOnly>();
  }

  [Fact]
  public async Task Handle_ShouldRequeueEveryUpload()
  {
    var processed = Processed(1);
    var failed = Failed(2);
    Uploads(processed, failed);

    await Handler().Handle(new RequeueCommitUploadsCommand(CommitId), Ct);

    _requeued.ShouldNotBeNull();
    _requeued.Select(static requeue => requeue.Upload).ShouldBe([processed, failed]);
    _requeued.ShouldAllBe(static requeue => requeue.StartingLog.MessageTotal == 0 && requeue.StartingLog.PathTotal == 0);
    A.CallTo(() => _reports.RequeueUploadsAsync(A<IReadOnlyList<UploadRequeue>>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // An upload that waits for its job, or that a worker holds, is parsed with the current parsers anyway.
  [Fact]
  public async Task Handle_UploadNotEnded_ShouldLeaveItAndItsRawFilesUnchecked()
  {
    var processed = Processed(1);
    var received = Received(2);
    Uploads(processed, received);

    await Handler().Handle(new RequeueCommitUploadsCommand(CommitId), Ct);

    _requeued.ShouldNotBeNull().ShouldHaveSingleItem().Upload.ShouldBe(processed);
    A.CallTo(() => _rawStore.ListExpiredAsync(A<IReadOnlyCollection<Guid>>.That.IsSameSequenceAs(new[] { processed.PublicId }), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NoEndedUpload_ShouldDoNothing()
  {
    Uploads(Received(1));

    await Handler().Handle(new RequeueCommitUploadsCommand(CommitId), Ct);

    A.CallTo(_reports).MustNotHaveHappened();
    A.CallTo(_rawStore).MustNotHaveHappened();
  }

  // All or nothing: a commit rebuilt from a part of its uploads would lose coverage without a word.
  [Fact]
  public async Task Handle_RawExpiredSinceTheRequest_ShouldThrowUnprocessableAndRequeueNothing()
  {
    var kept = Processed(1);
    var expired = Processed(2);
    Uploads(kept, expired);
    A.CallTo(() => _rawStore.ListExpiredAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns([expired.PublicId]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await Handler().Handle(new RequeueCommitUploadsCommand(CommitId), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.RawExpired);
    A.CallTo(_reports).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldThrowNotFound()
  {
    A.CallTo(() => _commits.FindAsync(CommitId, A<CancellationToken>._)).Returns((Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(new RequeueCommitUploadsCommand(CommitId), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
    A.CallTo(_reports).MustNotHaveHappened();
  }

  // The move off a synthetic merge commit is not done again: its message is the only one that survives.
  [Fact]
  public async Task Handle_UploadMovedOffAMergeCommit_ShouldKeepTheMessageOfTheMove()
  {
    var moved = Received(1);
    var rewrite = $"{UploadErrors.MergeCommitRewritten}: aaa -> bbb";
    moved.AddToLog(rewrite);
    moved.MarkProcessed(1, 1, 0, default, moved.ProcessingLog.Add("no file list: paths not verified").AddPath("src/unresolved.cs"), Now);
    Uploads(moved);

    await Handler().Handle(new RequeueCommitUploadsCommand(CommitId), Ct);

    var log = _requeued.ShouldNotBeNull().ShouldHaveSingleItem().StartingLog;

    log.Messages.ShouldBe([rewrite]);
    log.Paths.ShouldBeEmpty();
    log.PathTotal.ShouldBe(0);
  }

  private static Upload Received(int sequence) =>
    Upload.Receive(CommitId, sequence, $"key-{sequence}", [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);

  private static Upload Processed(int sequence)
  {
    var upload = Received(sequence);
    upload.MarkProcessed(1, 1, 0, default, ProcessingLog.Empty.Add("written by the processing").AddPath("src/unresolved.cs"), Now);

    return upload;
  }

  private static Upload Failed(int sequence)
  {
    var upload = Received(sequence);
    upload.MarkFailed(UploadErrors.ParseFailed, "Unreadable.", 1, 0, 0, ProcessingLog.Empty.Add("written by the processing"), Now);

    return upload;
  }

  private void Uploads(params Upload[] uploads) => A.CallTo(() => _uploads.ListByCommitAsync(CommitId, A<CancellationToken>._)).Returns(uploads);

  private RequeueCommitUploadsCommandHandler Handler() => new(_commits, _uploads, new UploadRequeuer(_reports, _rawStore));

  #endregion
}
