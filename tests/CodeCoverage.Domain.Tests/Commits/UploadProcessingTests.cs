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
