// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLogPrinterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Output;

public sealed class ProcessingLogPrinterTests
{
  #region Fields

  private readonly StringWriter _output = new();

  #endregion

  #region Methods

  [Fact]
  public void Print_Processed_ShouldPrintTheCountsAndTheUnresolvedPaths()
  {
    Print(Status(UploadStatus.Processed, 412, 3, ["src/a.cs", "src/b.cs", "src/c.cs"], 3));

    Lines().ShouldBe(["The upload was processed.", "resolved 412/415 files; unresolved: src/a.cs, src/b.cs, src/c.cs"]);
  }

  [Fact]
  public void Print_EverythingResolved_ShouldNotNameAnyPath()
  {
    Print(Status(UploadStatus.Processed, 415, 0, [], 0));

    Lines().ShouldBe(["The upload was processed.", "resolved 415/415 files"]);
  }

  // The server keeps 200 paths at most and tells how many there were.
  [Fact]
  public void Print_MorePathsThanPrinted_ShouldCountTheOthers()
  {
    string[] paths = [.. Enumerable.Range(1, 12).Select(static i => $"f{i}.cs")];

    Print(Status(UploadStatus.Processed, 5, 250, paths, 250));

    Lines()[1].ShouldBe("resolved 5/255 files; unresolved: f1.cs, f2.cs, f3.cs, f4.cs, f5.cs, f6.cs, f7.cs, f8.cs, f9.cs, f10.cs (+240 more)");
  }

  [Fact]
  public void Print_Verbose_ShouldPrintEveryPathTheServerKept()
  {
    string[] paths = [.. Enumerable.Range(1, 12).Select(static i => $"f{i}.cs")];

    Print(Status(UploadStatus.Processed, 5, 250, paths, 250), verbose: true);

    Lines()[1].ShouldEndWith("f11.cs, f12.cs (+238 more)");
  }

  // An ambiguous path is listed although its file counts as resolved or not: the names follow the list, not the count.
  [Fact]
  public void Print_PathsWithoutUnresolvedFile_ShouldStillNameThem()
  {
    Print(Status(UploadStatus.Processed, 3, 0, ["src/ambiguous.cs"], 1));

    Lines()[1].ShouldBe("resolved 3/3 files; unresolved: src/ambiguous.cs");
  }

  [Fact]
  public void Print_PathsNotKept_ShouldOnlyCountThem()
  {
    Print(Status(UploadStatus.Processed, 3, 2, [], 2));

    Lines()[1].ShouldBe("resolved 3/5 files; unresolved: (+2 more)");
  }

  [Fact]
  public void Print_Messages_ShouldPrintThemAndCountTheDroppedOnes()
  {
    var status = new UploadStatus
    {
      State = UploadStatus.Processed,
      ResolvedFiles = 1,
      Log = new UploadStatusLog { Messages = ["Upload.UnsupportedFormat: coverage.json", "the tree of the repository is truncated"], MessageTotal = 5 },
    };

    Print(status);

    Lines().ShouldBe(
      [
        "The upload was processed.",
        "resolved 1/1 files",
        "  Upload.UnsupportedFormat: coverage.json",
        "  the tree of the repository is truncated",
        "  (+3 more messages)",
      ]
    );
  }

  [Fact]
  public void Print_Failed_ShouldPrintTheReason()
  {
    var status = Status(UploadStatus.Failed, 0, 2, ["x.cs"], 2) with { Error = "No file of the reports was found in the repository." };

    Print(status);

    Lines()[0].ShouldBe("error: The processing of the upload failed: No file of the reports was found in the repository.");
    Lines()[1].ShouldBe("resolved 0/2 files; unresolved: x.cs (+1 more)");
  }

  [Fact]
  public void Print_FailedWithoutReasonNorLog_ShouldStillPrint()
  {
    Print(new UploadStatus { State = UploadStatus.Failed });

    Lines().ShouldBe(["error: The processing of the upload failed: the server gave no reason", "resolved 0/0 files"]);
  }

  private static UploadStatus Status(string state, int resolved, int unresolved, IReadOnlyList<string> paths, int pathTotal) =>
    new()
    {
      State = state,
      ResolvedFiles = resolved,
      UnresolvedFiles = unresolved,
      Log = new UploadStatusLog { Messages = [], Paths = paths, PathTotal = pathTotal },
    };

  private void Print(UploadStatus status, bool verbose = false) => ProcessingLogPrinter.Print(new ConsoleReporter(_output) { IsVerbose = verbose }, status);

  private string[] Lines() => _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

  #endregion
}
