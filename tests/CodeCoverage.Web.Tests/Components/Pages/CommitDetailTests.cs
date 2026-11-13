// ------------------------------------------------------------------------------------------------
// <copyright file="CommitDetailTests.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

using CommitDetailPage = CodeCoverage.Web.Components.Pages.CommitDetail;
using CommitStatus = CodeCoverage.Domain.Notifications.CommitStatus;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The summary of a commit (M3.25) against a fake sender.
public sealed class CommitDetailTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7));

  private static readonly GitSha BaseSha = GitSha.Parse("a1b2c3d" + new string('2', GitSha.Length - 7));

  private static readonly GitSha OriginSha = GitSha.Parse("9f8e7d6" + new string('3', GitSha.Length - 7));

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly Commit _commit;

  #endregion

  #region Ctors

  public CommitDetailTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

    _commit = Commit.Create(5, Sha, Now.AddMinutes(-12));
    _commit.MarkPending("feature/webhook-retry", 423, Now.AddMinutes(-12));
    _commit.RecordFinalization(CommitState.Complete, 3, new CoverageTotals(8123, 1877, 0, 0, 0), 2, 2, CarryforwardStatus.None, "default", Now);

    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Builtin, null, null, null, [], [])));
    Detail(CommitConfig.Create(5, Sha, ConfigSource.Commit, "codecov.yml", "coverage: {}", [new ConfigWarning(ConfigWarning.WithoutEffectKey, "coverage.status.project.default.if_ci_failed")], [], Now));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheSummaryOfTheCommit() => InEnglish(() =>
  {
    var cut = Render<CommitDetailPage>(Address());

    cut.Find(".cc-header__title").TextContent.ShouldContain("feat(alerts): retry policy");
    var subtitle = cut.Find(".cc-header__subtitle").TextContent;
    subtitle.ShouldContain(L("Commit_OnBranch", "e4f5a6b", "feature/webhook-retry"));
    subtitle.ShouldContain("jo-rivera");
    subtitle.ShouldContain("#423");
    cut.FindAll("a").Where(static a => a.TextContent.Contains("#423", StringComparison.Ordinal)).ShouldBeEmpty();
    var coverage = cut.FindAll(".cc-kpi")[0];
    coverage.QuerySelector(".cc-kpi__value")!.TextContent.ShouldBe("81.23%");
    coverage.QuerySelector(".cc-kpi__footer code")!.TextContent.ShouldBe("a1b2c3d");
    coverage.QuerySelector(".cc-badge")!.TextContent.ShouldContain("+1.23");
    cut.FindAll("[data-panel=flags] tbody tr").Select(static r => r.GetAttribute("data-flag")).ShouldBe(["backend", "frontend"]);
    cut.Find("[data-flag=frontend]").TextContent.ShouldContain("9f8e7d6");
    cut.Find("[data-flag=backend]").TextContent.ShouldContain(L("Commit_UploadOfThisCommit"));
    cut.Find("[data-panel=statuses]").TextContent.ShouldContain("coverage/project");
    cut.Find("[data-panel=statuses] a").GetAttribute("href").ShouldBe("https://coverage.example.com/github/octo-org/app/commit/" + Sha.Value);
    var config = cut.Find("[data-panel=config]").TextContent;
    config.ShouldContain(L("ConfigSource_Commit"));
    config.ShouldContain("codecov.yml");
    config.ShouldContain("coverage.status.project.default.if_ci_failed");
    config.ShouldNotContain("coverage: {}");
    cut.FindAll(".cc-config-error").ShouldBeEmpty();
  });

  // Uploads by sequence, whatever their state, then the carried sessions by number. An upload counts once the session
  // naming it by identifier exists: the stale carried session numbered like the second upload never makes it counted.
  [Fact]
  public void Render_Uploads_ShouldJoinSessionsByUploadId() => InEnglish(() =>
  {
    var counted = Upload.Receive(5, 1, "a", ["backend"], "dotnet-tests", "github-actions", "8812", null, "https://ci.example.com/8812", null, null, null, PrincipalKind.RepositoryToken, Now);
    counted.MarkProcessed(198, 198, 0, new CoverageTotals(9, 1, 0, 0, 0), ProcessingLog.Empty, Now);
    SetId(counted, 11);
    var waiting = Upload.Receive(5, 2, "b", ["backend"], null, "azure-pipelines", null, null, "javascript:alert(1)", null, null, null, PrincipalKind.RepositoryToken, Now);
    waiting.MarkProcessed(121, 119, 2, new CoverageTotals(9, 1, 0, 0, 0), ProcessingLog.Restore([], 0, ["obj/Generated.cs", "x.cs"], 2), Now);
    SetId(waiting, 12);
    var failed = Upload.Receive(5, 3, "c", ["backend"], "dotnet-e2e", null, null, null, null, null, null, null, PrincipalKind.RepositoryToken, Now);
    failed.MarkFailed("Upload.UnsupportedFormat", "No parser.", 1, 0, 0, ProcessingLog.Restore(["Upload.UnsupportedFormat: coverage.json"], 1, [], 0), Now);
    SetId(failed, 13);
    Session[] sessions =
    [
      Session.CarriedForward(9, 2, OriginSha, 1, 7, null, ["frontend"], "jest", null, null, new CoverageTotals(7, 3, 0, 0, 0)),
      Session.FromUpload(9, 1, 11, ["backend"], "dotnet-tests", "github-actions", null, new CoverageTotals(9, 1, 0, 0, 0)),
    ];
    var detail = new Domain.Commits.CommitDetail(_commit, sessions, [failed, waiting, counted], [], []);
    A.CallTo(() => _sender.Send(A<GetCommitQuery>.That.Matches(q => q.Sha == Sha), A<CancellationToken>._))
     .Returns(new ValueTask<Domain.Commits.CommitDetail>(detail));

    var cut = Render<CommitDetailPage>(Address());

    cut.FindAll("[data-panel=uploads] tbody tr").Select(static r => r.GetAttribute("data-row")).ShouldBe(["u1", "u2", "u3", "s2"]);
    cut.Find("[data-row=u1]").GetAttribute("data-status").ShouldBe("Processed");
    cut.Find("[data-row=u1] a").TextContent.ShouldContain("GitHub Actions #8812");
    cut.Find("[data-row=u2]").GetAttribute("data-status").ShouldBe("Processing");
    cut.Find("[data-row=u2]").QuerySelector("a").ShouldBeNull();
    cut.Find("[data-row=u2]").TextContent.ShouldContain("Azure Pipelines");
    cut.Find("[data-row=u2] [data-log=paths]").TextContent.ShouldContain("obj/Generated.cs");
    cut.Find("[data-row=u3]").GetAttribute("data-status").ShouldBe("Failed");
    cut.Find("[data-row=u3] [data-log=error]").TextContent.ShouldBe(L("Upload.UnsupportedFormat"));
    cut.Find("[data-row=s2]").GetAttribute("data-status").ShouldBe("CarriedForward");
    cut.Find("[data-row=s2] [data-log=origin] code").TextContent.ShouldBe("9f8e7d6");
  });

  // /commit/{sha} lies under no tab's address: the page imposes the commits tab.
  [Fact]
  public void Render_ShouldMarkTheCommitsTab()
  {
    Navigation.NavigateTo($"github/octo-org/app/commit/{Sha.Value}");

    var cut = Render<CommitDetailPage>(Address());

    cut.FindAll(".cc-pill-tabs__tab--active").ShouldHaveSingleItem().TextContent.Trim().ShouldBe(L("Tab_Commits"));
  }

  [Fact]
  public void Render_ConfigError_ShouldShowBanner()
  {
    Detail(CommitConfig.Create(5, Sha, ConfigSource.Organization, null, null, [], ["Line 12: unknown value for round."], Now));

    var cut = Render<CommitDetailPage>(Address());

    var banner = cut.Find(".cc-config-error");
    banner.TextContent.ShouldContain(L("Commit_ConfigErrorTitle"));
    banner.TextContent.ShouldContain("Line 12: unknown value for round.");
    cut.Find("[data-panel=config]").TextContent.ShouldContain(L("ConfigSource_Organization"));
  }

  // Without a stored configuration row, the source recorded on the commit is shown as it is.
  [Fact]
  public void Render_NoStoredConfig_ShouldFallBackToTheSourceOfTheCommit()
  {
    Detail(null);

    var cut = Render<CommitDetailPage>(Address());

    cut.Find("[data-panel=config] code").TextContent.ShouldBe("default");
  }

  // A short or malformed SHA is the inaccessible state, and nothing is read.
  [Theory]
  [InlineData("e4f5a6b")]
  [InlineData("not-a-sha")]
  public void Render_InvalidSha_ShouldShowInaccessibleStateWithoutReading(string sha)
  {
    var cut = Render<CommitDetailPage>(Address(sha));

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Fact]
  public void Render_NotFound_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetCommitQuery>._, A<CancellationToken>._)).ThrowsAsync(new NotFoundException(CommitErrors.NotFound, "Missing."));

    var cut = Render<CommitDetailPage>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
  }

  // The Files card gives the files of the base, the Sessions card the flags and origin of the carried sessions (mockup).
  [Fact]
  public void Render_ShouldShowTheBaseFilesAndTheCarriedOrigin() => InEnglish(() =>
  {
    var cut = Render<CommitDetailPage>(Address());

    var cards = cut.FindAll(".cc-kpi");
    cards[1].QuerySelector(".cc-kpi__footer")!.TextContent.ShouldContain(L("Commit_BaseFiles"));
    cards[1].QuerySelector(".cc-kpi__footer b")!.TextContent.ShouldBe("210");
    cards[3].QuerySelector(".cc-kpi__footer")!.TextContent.ShouldContain("frontend " + L("Commit_CarriedFrom"));
    cards[3].QuerySelector(".cc-kpi__footer code")!.TextContent.ShouldBe("9f8e7d6");
  });

  [Fact]
  public void Render_NoComparisonNorCarriedSession_ShouldShowNeitherFooter()
  {
    Detail(null, withComparison: false, withCarried: false);

    var cut = Render<CommitDetailPage>(Address());

    var cards = cut.FindAll(".cc-kpi");
    cards[1].QuerySelector(".cc-kpi__footer").ShouldBeNull();
    cards[3].QuerySelector(".cc-kpi__footer").ShouldBeNull();
  }

  // A navigation to another commit while the first is still loading: the late answer of the first is dropped.
  [Fact]
  public void Render_FirstLoadAnsweringLast_ShouldShowTheSecond() => InEnglish(() =>
  {
    var first = new TaskCompletionSource<Domain.Commits.CommitDetail>();
    A.CallTo(() => _sender.Send(A<GetCommitQuery>.That.Matches(q => q.Sha == Sha), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<Domain.Commits.CommitDetail>(first.Task));
    var other = Commit.Create(5, BaseSha, Now);
    A.CallTo(() => _sender.Send(A<GetCommitQuery>.That.Matches(q => q.Sha == BaseSha), A<CancellationToken>._))
     .Returns(new ValueTask<Domain.Commits.CommitDetail>(new Domain.Commits.CommitDetail(other, [], [], [], [])));
    var cut = Render<CommitDetailPage>(Address());

    cut.Render(static p => p.Add(static c => c.Sha, BaseSha.Value));
    cut.WaitForAssertion(() => cut.Find(".cc-header__title").TextContent.ShouldContain("a1b2c3d"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(new Domain.Commits.CommitDetail(_commit, [], [], [], [])));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find(".cc-header__title").TextContent.ShouldContain("a1b2c3d");
  });

  private static Action<ComponentParameterCollectionBuilder<CommitDetailPage>> Address(string? sha = null) =>
    p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app").Add(static c => c.Sha, sha ?? Sha.Value);

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

  private void Detail(CommitConfig? config, bool withComparison = true, bool withCarried = true)
  {
    var flags = new[]
    {
      CommitFlagTotals.Create(_commit, CommitFlagTotals.ProjectFlag, new CoverageTotals(8123, 1877, 0, 0, 0), false, null),
      CommitFlagTotals.Create(_commit, "frontend", new CoverageTotals(7002, 2998, 0, 0, 0), true, OriginSha),
      CommitFlagTotals.Create(_commit, "backend", new CoverageTotals(851, 149, 0, 0, 0), false, null),
    };
    var statuses = new[]
    {
      new PostedStatus(9, new CommitStatus("coverage/project", StatusState.Success, "81.23% (+1.23%) compared to a1b2c3d"), "https://coverage.example.com/github/octo-org/app/commit/" + Sha.Value, Now),
      new PostedStatus(9, new CommitStatus("coverage/patch", StatusState.Failure, "72.50% of diff hit"), "javascript:alert(1)", Now),
    };
    var comparison = new CommitComparison(
      9,
      null,
      null,
      BaseSha,
      1,
      1,
      DiffSource.Compare,
      new Comparison(new CoverageTotals(8123, 1877, 0, 0, 0), new CoverageTotals(80, 20, 0, 0, 0), PatchCoverage.Empty, [], [], [], DiffIncomplete: false),
      Now);
    var git = GitCommit.Create(5, new ForgeCommitInfo(Sha, [], "feat(alerts): retry policy\n\nBody.", "jo-rivera", Now.AddMinutes(-12)), Now);

    Session[] sessions = withCarried ? [Session.CarriedForward(9, 2, OriginSha, 1, 7, null, ["frontend"], "jest", null, null, new CoverageTotals(7, 3, 0, 0, 0))] : [];
    var detail = new Domain.Commits.CommitDetail(_commit, sessions, [], flags, statuses, git, config, withComparison ? comparison : null, withComparison ? 210 : null);

    A.CallTo(() => _sender.Send(A<GetCommitQuery>.That.Matches(q => q.Sha == Sha), A<CancellationToken>._))
     .Returns(new ValueTask<Domain.Commits.CommitDetail>(detail));
  }

  private static void SetId(object entity, long id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
