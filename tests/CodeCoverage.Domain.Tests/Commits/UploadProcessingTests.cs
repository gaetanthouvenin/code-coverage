// ------------------------------------------------------------------------------------------------
// <copyright file="UploadProcessingTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class UploadProcessingTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void MarkProcessed_ShouldRecordTheResult()
  {
    var upload = Received();

    upload.MarkProcessed(2, 3, 1, new CoverageTotals(4, 5, 6, 0, 0), ProcessingLog.Empty.Add("note"), Now);

    upload.State.ShouldBe(UploadState.Processed);
    upload.ReportFileCount.ShouldBe(2);
    upload.ResolvedFileCount.ShouldBe(3);
    upload.UnresolvedFileCount.ShouldBe(1);
    upload.Lines.ShouldBe(15);
    upload.Hits.ShouldBe(4);
    upload.Misses.ShouldBe(5);
    upload.Partials.ShouldBe(6);
    upload.ProcessedAt.ShouldBe(Now);
    upload.ProcessingLog.Messages.ShouldBe(["note"]);
  }

  [Fact]
  public void MarkFailed_ShouldRecordTheError()
  {
    var upload = Received();

    upload.MarkFailed("Upload.NoFileMapped", "Nothing matched.", 1, 0, 2, ProcessingLog.Empty, Now);

    upload.State.ShouldBe(UploadState.Failed);
    upload.ErrorCode.ShouldBe("Upload.NoFileMapped");
    upload.Error.ShouldBe("Nothing matched.");
    upload.UnresolvedFileCount.ShouldBe(2);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void Requeue_EndedUpload_ShouldForgetTheResultAndKeepWhatWasSent(bool failed)
  {
    var upload = Received();
    var publicId = upload.PublicId;

    if (failed)
    {
      upload.MarkFailed("Upload.ParseFailed", "Unreadable.", 1, 0, 2, ProcessingLog.Empty.Add("previous").AddPath("a.cs"), Now);
    }
    else
    {
      upload.MarkProcessed(2, 3, 1, new CoverageTotals(4, 5, 6, 0, 0), ProcessingLog.Empty.Add("previous").AddPath("a.cs"), Now);
    }

    upload.Requeue(ProcessingLog.Empty.Add("kept"));

    upload.State.ShouldBe(UploadState.Received);
    upload.ReportFileCount.ShouldBe(0);
    upload.ResolvedFileCount.ShouldBe(0);
    upload.UnresolvedFileCount.ShouldBe(0);
    upload.Lines.ShouldBe(0);
    upload.Hits.ShouldBe(0);
    upload.Misses.ShouldBe(0);
    upload.Partials.ShouldBe(0);
    upload.ErrorCode.ShouldBeNull();
    upload.Error.ShouldBeNull();
    upload.ProcessedAt.ShouldBeNull();
    upload.LockedBy.ShouldBeNull();
    upload.ProcessingLog.Messages.ShouldBe(["kept"]);
    upload.ProcessingLog.Paths.ShouldBeEmpty();

    upload.PublicId.ShouldBe(publicId);
    upload.ReceivedAt.ShouldBe(Now);
  }

  [Fact]
  public void Requeue_ThenProcessedAgain_ShouldRecordTheNewResult()
  {
    var upload = Received();
    upload.MarkFailed("Upload.ParseFailed", "Unreadable.", 1, 0, 0, ProcessingLog.Empty, Now);

    upload.Requeue(ProcessingLog.Empty);
    upload.MarkProcessed(1, 1, 0, new CoverageTotals(1, 0, 0, 0, 0), ProcessingLog.Empty, Now.AddMinutes(1));

    upload.State.ShouldBe(UploadState.Processed);
    upload.ErrorCode.ShouldBeNull();
    upload.ProcessedAt.ShouldBe(Now.AddMinutes(1));
  }

  [Fact]
  public void Requeue_UploadNotEnded_ShouldThrow()
  {
    var upload = Received();

    Should.Throw<InvalidOperationException>(() => upload.Requeue(ProcessingLog.Empty));
    upload.State.ShouldBe(UploadState.Received);
  }

  [Fact]
  public void Requeue_WithoutLog_ShouldThrow()
  {
    var upload = Received();
    upload.MarkFailed("Code.A", "text", 1, 0, 0, ProcessingLog.Empty, Now);

    Should.Throw<ArgumentNullException>(() => upload.Requeue(null!));
    upload.State.ShouldBe(UploadState.Failed);
  }

  [Fact]
  public void MarkProcessed_AlreadyProcessed_ShouldThrow()
  {
    var upload = Received();
    upload.MarkProcessed(1, 1, 0, default, ProcessingLog.Empty, Now);

    Should.Throw<InvalidOperationException>(() => upload.MarkProcessed(1, 1, 0, default, ProcessingLog.Empty, Now));
  }

  [Fact]
  public void MarkFailed_AlreadyFailed_ShouldThrow()
  {
    var upload = Received();
    upload.MarkFailed("Code.A", "text", 1, 0, 0, ProcessingLog.Empty, Now);

    Should.Throw<InvalidOperationException>(() => upload.MarkFailed("Code.A", "text", 1, 0, 0, ProcessingLog.Empty, Now));
  }

  [Theory]
  [InlineData("", "text")]
  [InlineData("code", " ")]
  public void MarkFailed_BlankText_ShouldThrow(string code, string error)
  {
    Should.Throw<ArgumentException>(() => Received().MarkFailed(code, error, 1, 0, 0, ProcessingLog.Empty, Now));
  }

  private static Upload Received() => Upload.Receive(9, 1, "key", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);

  #endregion
}
