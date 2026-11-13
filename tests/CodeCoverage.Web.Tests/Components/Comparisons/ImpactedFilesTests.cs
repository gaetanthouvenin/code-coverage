// ------------------------------------------------------------------------------------------------
// <copyright file="ImpactedFilesTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Web.Components.Comparisons;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Tests.Components.Comparisons;

public sealed class ImpactedFilesTests : UiTestContext
{
  #region Methods

  // A file links to its annotated source at the head, with the pull request, each segment escaped.
  [Fact]
  public void Click_File_ShouldLinkToSourceWithPull()
  {
    var sha = GitSha.Parse(new string('e', GitSha.Length));
    var href = ComparisonMapper.SourceHref("github/octo-org/app", sha, "src/My File#1.cs", 423);

    var cut = Render([File("src/My File#1.cs", href)]);

    cut.Find("[data-path='src/My File#1.cs'] a").GetAttribute("href")
       .ShouldBe($"github/octo-org/app/commit/{sha.Value}/blob/src/My%20File%231.cs?pull=423");
  }

  // As the comment: missed patch lines first, then the size of the change, then the ordinal path (« B » before « a »).
  [Fact]
  public void Render_ShouldSortByMissedPatchLines()
  {
    var sha = GitSha.Parse(new string('e', GitSha.Length));
    ImpactedFile[] files =
    [
      Impacted("src/a.cs", missed: 2, headHits: 9),
      Impacted("src/B.cs", missed: 2, headHits: 9),
      Impacted("src/small.cs", missed: 2, headHits: 6),
      Impacted("src/big.cs", missed: 2, headHits: 1),
      Impacted("src/most.cs", missed: 5, headHits: 9),
      Impacted("src/none.cs", missed: 0, headHits: 1),
    ];
    var result = new Comparison(new CoverageTotals(1, 0, 0, 0, 0), new CoverageTotals(1, 0, 0, 0, 0), PatchCoverage.Empty, files, [], [], false);
    var view = new ComparisonView(sha, new CommitComparison(9, 7, sha, sha, 1, 1, DiffSource.PullFiles, result, DateTimeOffset.UnixEpoch), null, false);
    var config = CoverageConfig.Create(ConfigNode.FromMap());

    var comparison = ComparisonMapper.ToViewModel(view, "main", "#7", null, config, "github/octo-org/app", 7, null);
    var cut = Render([.. comparison.Files]);

    cut.FindAll("tbody tr").Select(static r => r.GetAttribute("data-path"))
       .ShouldBe(["src/most.cs", "src/big.cs", "src/small.cs", "src/B.cs", "src/a.cs", "src/none.cs"]);
  }

  // A removed file links nowhere; a new one says so; a file with no coverable changed line shows ø.
  [Fact]
  public void Render_RemovedAndNewFiles_ShouldShowTheirMarkers()
  {
    var cut = Render([File("src/Gone.cs", null, removed: true), File("src/New.cs", "x", isNew: true, patch: null)]);

    cut.Find("[data-path='src/Gone.cs']").QuerySelector("a").ShouldBeNull();
    cut.Find("[data-path='src/Gone.cs'] s").TextContent.ShouldBe("src/Gone.cs");
    cut.Find("[data-path='src/New.cs']").TextContent.ShouldContain(L("Comparison_New"));
    cut.Find("[data-path='src/New.cs'] td:nth-child(3)").TextContent.Trim().ShouldBe("ø");
  }

  // Every file is listed, 50 a page.
  [Fact]
  public void Render_MoreThanAPage_ShouldPage()
  {
    var cut = Render([.. Enumerable.Range(0, 51).Select(static i => File($"src/F{i:00}.cs", "x"))]);

    cut.FindAll("tbody tr").Count.ShouldBe(50);
    cut.FindAll("nav.cc-panel__foot").ShouldHaveSingleItem();
  }

  // The base covers 10 of 10 lines: the fewer hits at the head, the larger the change.
  private static ImpactedFile Impacted(string path, int missed, int headHits) =>
    new(path, null, FileDiffStatus.Modified, new CoverageTotals(10, 0, 0, 0, 0), new CoverageTotals(headHits, 10 - headHits, 0, 0, 0), new CoverageTotals(1, missed, 0, 0, 0));

  private static ImpactedFileViewModel File(string path, string? href, bool removed = false, bool isNew = false, string? patch = "50.00%") =>
    new(path, href, isNew, removed, removed ? "—" : "80.00%", removed ? null : "+1.00", 1, patch, 1);

  private IRenderedComponent<ImpactedFiles> Render(IReadOnlyList<ImpactedFileViewModel> files) =>
    Render<ImpactedFiles>(p => p.Add(static c => c.Files, files));

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
