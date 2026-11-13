// ------------------------------------------------------------------------------------------------
// <copyright file="UploadSessionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Commits;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Tests.Components.Commits;

public sealed class UploadSessionsTests : UiTestContext
{
  #region Ctors

  public UploadSessionsTests()
  {
    Services.AddFakeSenderRunner(A.Fake<Mediator.ISender>());
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_CarriedForward_ShouldShowOrigin()
  {
    var cut = Render([Row("s4", 4, UploadStatus.CarriedForward, name: "jest", carriedFrom: "9f8e7d6")]);

    var row = cut.Find("[data-row=s4]");
    row.TextContent.ShouldContain(L("Session_CarriedForward"));
    row.QuerySelector("[data-log=origin] code")!.TextContent.ShouldBe("9f8e7d6");
    row.QuerySelector("td:nth-child(4)")!.TextContent.Trim().ShouldBe("—");
  }

  // The first paths are shown, the others behind a disclosure, and a truncated log never claims to be complete.
  [Fact]
  public void Render_Unresolved_ShouldListPaths()
  {
    var log = new ProcessingLogViewModel([], 0, [@"D:\a\1\s\obj\Generated.cs", "b.cs", "c.cs", "d.cs", "e.cs"], 7);

    var cut = Render([Row("u1", 1, UploadStatus.Processed, log: log)]);

    var paths = cut.Find("[data-log=paths]");
    paths.QuerySelector(".cc-badge")!.TextContent.ShouldContain(L("Upload_UnresolvedPaths", 12));
    paths.QuerySelectorAll(":scope > ul li").Select(static l => l.TextContent).ShouldBe([@"D:\a\1\s\obj\Generated.cs", "b.cs", "c.cs"]);
    paths.QuerySelectorAll("details li").Select(static l => l.TextContent).ShouldBe(["d.cs", "e.cs"]);
    paths.QuerySelector("[data-log=truncated-paths]")!.TextContent.ShouldBe(L("Upload_LogTruncated", 7));
    cut.FindAll("[data-log=resolved]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_ProcessedWithoutPath_ShouldSayAllResolved()
  {
    var cut = Render([Row("u1", 1, UploadStatus.Processed, files: "198", resolved: 198)]);

    cut.Find("[data-log=resolved]").TextContent.ShouldBe(L("Upload_PathsResolved", 198));
    cut.Find("[data-row=u1] td:nth-child(4)").TextContent.Trim().ShouldBe("198");
  }

  [Fact]
  public void Render_Failed_ShouldTranslateTheErrorCode()
  {
    var cut = Render([Row("u3", 3, UploadStatus.Failed, errorCode: "Upload.NoFileMapped")]);

    var row = cut.Find("[data-row=u3]");
    row.QuerySelector("td:nth-child(5) .cc-badge")!.TextContent.ShouldContain(L("Upload_Failed"));
    row.QuerySelector("[data-log=error]")!.TextContent.ShouldBe(L("Upload.NoFileMapped"));
  }

  // Decision D8: a message with a known code is translated, any other is shown as stored.
  [Fact]
  public void Render_Messages_ShouldTranslateKnownCodesOnly()
  {
    var log = new ProcessingLogViewModel(
      [new LogMessageViewModel("Upload.UnsupportedFormat", "coverage.json"), new LogMessageViewModel(null, "The report x.xml could not be read: IOException."), new LogMessageViewModel("Upload.Unknown", "z")],
      2,
      [],
      0);

    var cut = Render([Row("u1", 1, UploadStatus.Failed, errorCode: "Upload.ParseFailed", log: log)]);

    cut.FindAll("[data-log=messages] li").Select(static l => l.TextContent).ShouldBe(
      [$"{L("Upload.UnsupportedFormat")} (coverage.json)", "The report x.xml could not be read: IOException.", "Upload.Unknown: z"]);
    cut.Find("[data-log=truncated-messages]").TextContent.ShouldBe(L("Upload_LogTruncated", 2));
  }

  // The address of the build is written by the uploader: it opens without giving the console to the opened page.
  [Fact]
  public void Render_BuildUrl_ShouldOpenWithoutOpener()
  {
    var cut = Render([Row("u1", 1, UploadStatus.Processing, ci: "GitHub Actions #8812", ciUrl: "https://ci.example.com/8812")]);

    var link = cut.Find("[data-row=u1] a");
    link.GetAttribute("href").ShouldBe("https://ci.example.com/8812");
    link.GetAttribute("rel").ShouldBe("noopener noreferrer");
    link.TextContent.ShouldContain("GitHub Actions #8812");
    cut.Find("[data-row=u1]").TextContent.ShouldContain(L("Commit_Pending"));
  }

  [Fact]
  public void Render_NoName_ShouldShowTheNumber()
  {
    var cut = Render([Row("u2", 2, UploadStatus.Processing)]);

    cut.Find("[data-row=u2] strong").TextContent.ShouldBe(L("Upload_Number", 2));
    cut.Find("[data-row=u2] td:nth-child(2)").TextContent.Trim().ShouldBe("—");
  }

  [Fact]
  public void Render_NoRow_ShouldSayNoUploadYet()
  {
    var cut = Render([]);

    cut.Find("[data-panel=uploads]").TextContent.ShouldContain(L("Commit_NoUploadYet"));
    cut.FindAll("table").ShouldBeEmpty();
  }

  private static UploadSessionRowViewModel Row(
    string key,
    int number,
    UploadStatus status,
    string? name = null,
    string? ci = null,
    string? ciUrl = null,
    string? files = null,
    int resolved = 0,
    string? carriedFrom = null,
    string? errorCode = null,
    ProcessingLogViewModel? log = null) =>
    new(key, number, name, status, status is UploadStatus.CarriedForward ? null : Guid.NewGuid(), ci, ciUrl, ["backend"], files, resolved, carriedFrom, errorCode, log ?? ProcessingLogViewModel.Empty);

  private IRenderedComponent<UploadSessions> Render(IReadOnlyList<UploadSessionRowViewModel> rows) =>
    Render<UploadSessions>(p => p.Add(static c => c.Rows, rows));

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
