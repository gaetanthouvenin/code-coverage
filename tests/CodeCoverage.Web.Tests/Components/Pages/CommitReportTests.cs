// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Queries.GetCommit;
using CodeCoverage.Application.Features.Commits.Queries.GetCommitFiles;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepositoryPermission;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The detailed report of a commit (M5.15) against a fake sender.
public sealed class CommitReportTests : UiTestContext
{
  #region Fields

  private const string Dash = "—";

  private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7));

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly Commit _commit;

  #endregion

  #region Ctors

  public CommitReportTests()
  {
    Services.AddFakeSenderRunner(_sender);

    _commit = Commit.Create(5, Sha, Now.AddMinutes(-12));
    _commit.MarkPending("feature/webhook-retry", null, Now.AddMinutes(-12));
    _commit.RecordFinalization(CommitState.Complete, 4, new CoverageTotals(26, 8, 0, 0, 0), 2, 2, CarryforwardStatus.None, "default", Now);

    var target = ConfigNode.FromMap(("target", ConfigNode.FromText("80%")));
    var root = ConfigNode.FromMap(("coverage", ConfigNode.FromMap(("status", ConfigNode.FromMap(("project", ConfigNode.FromMap(("default", target))))))));
    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(root), ConfigSource.Builtin, null, null, null, [], [])));
    A.CallTo(() => _sender.Send(A<GetCommitQuery>._, A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<Domain.Commits.CommitDetail>(new Domain.Commits.CommitDetail(_commit, [], [], [CommitFlagTotals.Create(_commit, "backend", default, false, null)], [])));
    Files(
      null,
      File("src/a.cs", 8, 2, new CoverageDetail(9, 10, 1, 4, 2, 3)),
      File("src/b.cs", 5, 5, new CoverageDetail(5, 10, 0, 0, 1, 1)),
      File("src/c.cs", 10, 0, new CoverageDetail(10, 10, 2, 2, 0, 2)),
      File("web/d.js", 3, 1, new CoverageDetail(3, 4, 0, 0, null, null)));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowCodecovFirstThenThreeGroups() => InEnglish(() =>
  {
    var cut = Render<CommitReport>(Address());

    cut.FindAll(".cc-kpi__label").Select(static l => l.TextContent).ShouldBe([L("Report_CodecovCard"), L("Report_Lines"), L("Report_Branches"), L("Report_Methods")]);
    cut.FindAll(".cc-kpi")[0].ClassList.ShouldContain("cc-kpi--accent");
    cut.FindAll("thead tr:first-child th").Select(static h => h.TextContent.Trim()).ShouldBe([L("Column_File"), "Codecov", L("Report_Lines"), L("Report_Branches"), L("Report_Methods")]);
    Paths(cut).ShouldBe(["src/b.cs", "web/d.js", "src/a.cs", "src/c.cs"]);
    var row = cut.Find("tr[data-path='src/a.cs']").QuerySelectorAll("td");
    row[1].TextContent.ShouldBe("80.00%");
    row[2].TextContent.Trim().ShouldBe("9 / 10");
    row[3].TextContent.ShouldContain("90.00%");
    row[4].TextContent.Trim().ShouldBe("1 / 4");
    row[6].TextContent.Trim().ShouldBe("2 / 3");
    var total = cut.Find("tr[data-row=total]").QuerySelectorAll("td");
    total[0].TextContent.ShouldContain(L("Report_FileCount", 4));
    total[1].TextContent.ShouldBe("76.47%");
    total[2].TextContent.Trim().ShouldBe("27 / 34");
    cut.FindAll(".cc-kpi__value")[2].TextContent.ShouldBe("50.00%");
  });

  // The help of the header opens the section of the documentation that explains the four figures.
  [Fact]
  public void Render_ShouldLinkTheHelpToTheDocumentation()
  {
    var cut = Render<CommitReport>(Address());

    cut.Find("a.cc-help").GetAttribute("href").ShouldBe(DocumentationLinks.DetailedReport);
  }

  [Fact]
  public void SortHeader_ClickedTwice_ShouldReverseOrder()
  {
    var cut = Render<CommitReport>(Address());

    cut.Find("button[data-sort=lines]").Click();

    Paths(cut).ShouldBe(["src/b.cs", "web/d.js", "src/a.cs", "src/c.cs"]);
    cut.Find("button[data-sort=lines]").ParentElement!.GetAttribute("aria-sort").ShouldBe("ascending");

    cut.Find("button[data-sort=lines]").Click();

    Paths(cut).ShouldBe(["src/c.cs", "src/a.cs", "web/d.js", "src/b.cs"]);
    cut.Find("button[data-sort=lines]").ParentElement!.GetAttribute("aria-sort").ShouldBe("descending");
    cut.Find("button[data-sort=codecov]").ParentElement!.GetAttribute("aria-sort").ShouldBe("none");
    Navigation.Uri.ShouldContain("sort=lines%3Adesc");
  }

  // A file without branch has no percentage to sort by: it stays after the others in both directions.
  [Fact]
  public void Sort_DashValues_ShouldStayLast()
  {
    var cut = Render<CommitReport>(Address());

    cut.Find("button[data-sort=branches]").Click();

    Paths(cut).ShouldBe(["src/a.cs", "src/c.cs", "src/b.cs", "web/d.js"]);

    cut.Find("button[data-sort=branches]").Click();

    Paths(cut).ShouldBe(["src/c.cs", "src/a.cs", "src/b.cs", "web/d.js"]);
  }

  [Fact]
  public void Filter_UncoveredBranches_ShouldHideOtherFiles()
  {
    var cut = Render<CommitReport>(Address());

    cut.Find(".cc-report__filter select").Change(CommitReport.FilterUncoveredBranches);

    Paths(cut).ShouldBe(["src/a.cs"]);
    cut.Find("tr[data-row=total]").QuerySelectorAll("td")[0].TextContent.ShouldContain(L("Report_FileCount", 4));
    Navigation.Uri.ShouldContain("filter=uncovered-branches");
  }

  [Fact]
  public void Filter_BelowTarget_ShouldKeepTheFilesUnderTheProjectTarget()
  {
    var cut = Render<CommitReport>(Address());

    cut.Find(".cc-report__filter select").Change(CommitReport.FilterBelowTarget);

    Paths(cut).ShouldBe(["src/b.cs", "web/d.js"]);
  }

  [Fact]
  public void Search_NoMatch_ShouldShowEmptyState()
  {
    var cut = Render<CommitReport>(Address());

    cut.Find("input[type=search]").Input("nothing");

    Paths(cut).ShouldBeEmpty();
    cut.Find("[data-report=nomatch]").TextContent.ShouldContain(L("Report_NoMatchTitle"));
  }

  // A shared address opens the same view: its sort and its filter are read from the query string.
  [Fact]
  public void Render_SortAndFilterInAddress_ShouldOpenTheSameView()
  {
    Navigation.NavigateTo($"github/octo-org/app/commit/{Sha.Value}/report?sort=path:desc&filter=unexecuted-methods");

    var cut = Render<CommitReport>(Address());

    Paths(cut).ShouldBe(["src/c.cs", "src/a.cs"]);
    cut.Find("button[data-sort=path]").ParentElement!.GetAttribute("aria-sort").ShouldBe("descending");
  }

  [Fact]
  public void Render_Flag_ShouldReadTheFilesOfTheFlag()
  {
    Files("backend", File("api/Only.cs", 1, 0, new CoverageDetail(1, 1, 0, 0, 1, 1)));
    Navigation.NavigateTo($"github/octo-org/app/commit/{Sha.Value}/report?flag=backend");

    var cut = Render<CommitReport>(Address());

    Paths(cut).ShouldBe(["api/Only.cs"]);
    cut.Find(".cc-report__flag option[selected]").GetAttribute("value").ShouldBe("backend");
  }

  [Fact]
  public void Render_Cfi1_ShouldShowBannerAndDashes() => InEnglish(() =>
  {
    Files(null, File("src/a.cs", 8, 2, null), File("src/b.cs", 5, 5, null));
    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>._, A<CancellationToken>._)).Returns(new ValueTask<bool>(true));

    var cut = Render<CommitReport>(Address());

    cut.Find(".cc-report__legacy").TextContent.ShouldContain(L("Report_LegacyTitle"));
    cut.Find(".cc-report__legacy .cc-action-reprocess").TextContent.ShouldContain(L("Action_Reprocess"));
    cut.FindAll(".cc-kpi__value").Select(static v => v.TextContent).ShouldBe(["65.00%", Dash, Dash, Dash]);
    cut.FindAll(".cc-kpi__footer")[1].TextContent.ShouldBe(L("Report_ReprocessFoot"));
    var row = cut.Find("tr[data-path='src/a.cs']").QuerySelectorAll("td");
    row[1].TextContent.ShouldBe("80.00%");
    row.Skip(2).Select(static c => c.TextContent.Trim()).ShouldAllBe(static t => t == Dash);
    cut.FindAll(".cc-report__filter option").Select(static o => o.GetAttribute("value")).ShouldBe([CommitReport.FilterAll, CommitReport.FilterBelowTarget]);
  });

  [Fact]
  public void Render_Cfi1Reader_ShouldShowNoReprocess()
  {
    Files(null, File("src/a.cs", 8, 2, null));

    var cut = Render<CommitReport>(Address());

    cut.Find(".cc-report__legacy");
    cut.FindAll(".cc-action-reprocess").ShouldBeEmpty();
  }

  [Fact]
  public void Render_UnknownMethods_ShouldShowDash()
  {
    var cut = Render<CommitReport>(Address());

    var row = cut.Find("tr[data-path='web/d.js']").QuerySelectorAll("td");
    row[6].TextContent.Trim().ShouldBe(Dash);
    row[7].TextContent.Trim().ShouldBe(Dash);
    var total = cut.Find("tr[data-row=total]").QuerySelectorAll("td");
    total[6].TextContent.Trim().ShouldBe(Dash);
    cut.FindAll(".cc-kpi__value")[3].TextContent.ShouldBe(Dash);
    cut.FindAll(".cc-kpi__footer")[3].TextContent.ShouldBe(L("Report_NoMethods"));
  }

  [Fact]
  public void Render_Compacted_ShouldShowItsStateWithoutReadingFiles()
  {
    typeof(Commit).GetProperty(nameof(Commit.State))!.SetValue(_commit, CommitState.Compacted);

    var cut = Render<CommitReport>(Address());

    cut.Find("[data-state=compacted]").TextContent.ShouldContain(L("Source_CompactedTitle"));
    A.CallTo(() => _sender.Send(A<GetCommitFilesQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // A reader of another repository, or a missing commit, gets the inaccessible state, as on the commit page.
  [Fact]
  public void Render_NotFound_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetCommitQuery>._, A<CancellationToken>._)).ThrowsAsync(new NotFoundException(CommitErrors.NotFound, "Missing."));

    var cut = Render<CommitReport>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
  }

  private static FileTotals File(string path, int hits, int misses, CoverageDetail? detail) =>
    new(path, new CoverageTotals(hits, misses, 0, 0, 0), detail);

  private static IEnumerable<string?> Paths(IRenderedComponent<CommitReport> cut) =>
    cut.FindAll("tbody tr[data-path]").Select(static r => r.GetAttribute("data-path"));

  private static Action<ComponentParameterCollectionBuilder<CommitReport>> Address() =>
    p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app").Add(static c => c.Sha, Sha.Value);

  private static void InEnglish(Action test)
  {
    var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

    try
    {
      test();
    }
    finally
    {
      (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
    }
  }

  private void Files(string? flag, params FileTotals[] files) =>
    A.CallTo(() => _sender.Send(A<GetCommitFilesQuery>.That.Matches(q => q.Flag == flag), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<FileTotals>>(files));

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
