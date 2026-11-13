// ------------------------------------------------------------------------------------------------
// <copyright file="FileTreeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Features.Commits.Queries.GetCommitFiles;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Web.Components.Commits;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Tests.Components.Commits;

public sealed class FileTreeTests : UiTestContext
{
  #region Fields

  private static readonly string Sha = "e4f5a6b" + new string('1', GitSha.Length - 7);

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public FileTreeTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Files(null, File("src/Billing.Domain/Alerts/DeliveryPolicy.cs", 70, 2, 2), File("src/Billing.Domain/Alerts/Send.cs", 46, 11, 3), File("README.md", 0, 0, 0), File("tools/build.cs", 1, 9, 0));
  }

  #endregion

  #region Methods

  // A compacted commit keeps no index: the panel says so and asks nothing.
  [Fact]
  public void Render_Compacted_ShouldShowHintWithoutReading()
  {
    var cut = Render(compacted: true);

    cut.Find("[data-files=compacted]").TextContent.ShouldBe(L("Commit_CompactedHint"));
    A.CallTo(() => _sender.Send(A<GetCommitFilesQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Rule 1: a folder sums the lines of its files, never averages their percentages; a chain of single folders shows once.
  [Fact]
  public void Render_Folder_ShouldSumItsFiles()
  {
    var cut = Render();

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path")).ShouldBe(["README.md", "src/Billing.Domain/Alerts", "tools"]);
    var folder = cut.Find("[data-path='src/Billing.Domain/Alerts']");
    folder.QuerySelector(".cc-files__toggle")!.TextContent.ShouldContain("src/Billing.Domain/Alerts");
    folder.QuerySelector(".cc-files__toggle")!.GetAttribute("aria-expanded").ShouldBe("false");
    folder.QuerySelectorAll("td.cc-num").Select(static c => c.TextContent).ShouldBe(["134", "116", "5", "13"]);
    folder.QuerySelector(".cc-covbar")!.TextContent.ShouldContain(86.57m.ToString("N2", CultureInfo.CurrentCulture));
  }

  [Fact]
  public void Click_Folder_ShouldShowItsFiles()
  {
    var cut = Render();

    cut.Find("[data-path='src/Billing.Domain/Alerts'] button").Click();

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path"))
       .ShouldBe(["README.md", "src/Billing.Domain/Alerts", "src/Billing.Domain/Alerts/DeliveryPolicy.cs", "src/Billing.Domain/Alerts/Send.cs", "tools"]);
    cut.Find("[data-path='src/Billing.Domain/Alerts/Send.cs'] td").GetAttribute("style").ShouldBe("padding-left:36px");
  }

  // The coverage order applies within each level; a node without line comes last; the path breaks the ties.
  [Fact]
  public void Click_SortByCoverage_ShouldOrderEachLevel()
  {
    var cut = Render();

    cut.Find(".cc-sort").Click();

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path")).ShouldBe(["tools", "src/Billing.Domain/Alerts", "README.md"]);
    cut.Find("th[aria-sort]").GetAttribute("aria-sort").ShouldBe("ascending");

    cut.Find(".cc-sort").Click();

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path")).ShouldBe(["src/Billing.Domain/Alerts", "tools", "README.md"]);
  }

  [Fact]
  public void Search_ShouldListMatchingFilesFlat()
  {
    var cut = Render();

    cut.Find("input[type=search]").Input("alerts/");

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path"))
       .ShouldBe(["src/Billing.Domain/Alerts/DeliveryPolicy.cs", "src/Billing.Domain/Alerts/Send.cs"]);
  }

  [Fact]
  public void ChangeFlag_ShouldReload()
  {
    Files("backend", File("api/Only.cs", 1, 0, 0));
    var cut = Render();

    cut.Find("select").Change("backend");

    cut.WaitForAssertion(() => cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path")).ShouldBe(["api"]));
    A.CallTo(() => _sender.Send(A<GetCommitFilesQuery>.That.Matches(static q => q.Flag == "backend"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    cut.FindAll("select option").Select(static o => o.TextContent).ShouldBe([L("Filter_AllFlags"), "backend", "frontend"]);
  }

  // The tree of the first flag answering after the second never replaces it.
  [Fact]
  public void ChangeFlag_FirstAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<IReadOnlyList<FileTotals>>();
    A.CallTo(() => _sender.Send(A<GetCommitFilesQuery>.That.Matches(static q => q.Flag == "backend"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<FileTotals>>(first.Task));
    Files("frontend", File("web/App.ts", 1, 0, 0));
    var cut = Render();

    cut.Find("select").Change("backend");
    cut.Find("select").Change("frontend");
    cut.WaitForAssertion(() => cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path")).ShouldBe(["web"]));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult([File("api/Late.cs", 1, 0, 0)]));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path")).ShouldBe(["web"]);
  }

  [Fact]
  public void Render_NotFinalized_ShouldSayNoFileYet()
  {
    Files(null);

    var cut = Render();

    cut.Find("[data-files=empty]").TextContent.ShouldBe(L("Commit_NoFileYet"));
  }

  private static FileTotals File(string path, int hits, int misses, int partials) => new(path, new CoverageTotals(hits, misses, partials, 0, 0));

  private void Files(string? flag, params FileTotals[] files) =>
    A.CallTo(() => _sender.Send(A<GetCommitFilesQuery>.That.Matches(q => q.Flag == flag), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<FileTotals>>(files));

  private IRenderedComponent<FileTree> Render(bool compacted = false) =>
    Render<FileTree>(p => p.Add(static c => c.Forge, "github")
                           .Add(static c => c.Owner, "octo-org")
                           .Add(static c => c.Name, "app")
                           .Add(static c => c.Sha, Sha)
                           .Add(static c => c.Flags, ["backend", "frontend"])
                           .Add(static c => c.Format, new CoverageFormatViewModel(2, "Nearest"))
                           .Add(static c => c.Compacted, compacted));

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
