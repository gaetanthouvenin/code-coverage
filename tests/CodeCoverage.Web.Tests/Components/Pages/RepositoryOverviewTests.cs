// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryOverviewTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Commits.Queries.ListCommits;
using CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;
using CodeCoverage.Application.Features.Repositories.Queries.GetBadgeToken;
using CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepository;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

using OverviewPage = CodeCoverage.Web.Components.Pages.RepositoryOverview;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The overview of a repository (M3.21) against a fake sender.
public sealed class RepositoryOverviewTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha HeadSha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha OriginSha = GitSha.Parse("9f8e7d6" + new string('0', GitSha.Length - 7));

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly Commit _head;

  private readonly TestOptionsMonitor<UrlOptions> _urls = new(new UrlOptions
  {
    ApiPublicUrl = "https://coverage-api.example.com",
    WebPublicUrl = "https://coverage.example.com",
  });

  #endregion

  #region Ctors

  public RepositoryOverviewTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
    Services.AddSingleton<IOptionsMonitor<UrlOptions>>(_urls);

    _head = Commit.Create(5, HeadSha, Now);
    _head.MarkPending("main", null, Now);
    _head.RecordFinalization(CommitState.Complete, 1, new CoverageTotals(8241, 1759, 0, 0, 0), 1, 1, CarryforwardStatus.None, "default", Now);

    Overview(
      ["backend", "frontend"],
      CommitFlagTotals.Create(_head, CommitFlagTotals.ProjectFlag, new CoverageTotals(8247, 1753, 0, 0, 0), false, null),
      CommitFlagTotals.Create(_head, "backend", new CoverageTotals(8519, 1481, 0, 0, 0), false, null),
      CommitFlagTotals.Create(_head, "frontend", new CoverageTotals(6999, 3001, 0, 0, 0), true, OriginSha)
    );
    Config(ConfigNode.FromMap(("coverage", ConfigNode.FromMap(("precision", ConfigNode.FromNumber(1)), ("round", ConfigNode.FromText("down"))))));
    A.CallTo(() => _sender.Send(A<GetCoverageHistoryQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call => new ValueTask<IReadOnlyList<CoverageSeries>>([.. call.GetArgument<GetCoverageHistoryQuery>(0)!.Flags.Select(Series)]));
    Activity();
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheProjectAndEveryFlagWithTheirChange() => InEnglish(() =>
  {
    var cut = Render<OverviewPage>(Address());

    var cards = cut.FindAll(".cc-kpi");
    cards.Select(static c => c.GetAttribute("aria-label")).ShouldBe([L("Flag_Project"), "backend", "frontend"]);
    cards[0].ClassList.ShouldContain("cc-kpi--accent");
    cards[0].QuerySelector(".cc-kpi__value")!.TextContent.ShouldBe("82.4%");
    cards[0].QuerySelector(".cc-badge")!.TextContent.ShouldContain("↑");
    cards[0].QuerySelector(".cc-badge")!.TextContent.ShouldContain("1.2 pt");
    cards[0].QuerySelector(".cc-kpi__footer")!.TextContent.ShouldContain(L("Card_Change30Days"));
    cards[2].QuerySelector(".cc-kpi__footer")!.TextContent.ShouldContain(L("Card_CarriedForwardFrom"));
    cards[2].QuerySelector(".cc-kpi__footer code")!.TextContent.ShouldBe("9f8e7d6");
    cut.Find(".cc-header__subtitle").TextContent.ShouldBe(L("Repository_DefaultBranch", "main"));
    cut.FindAll(".cc-breadcrumb a").Select(static a => a.TextContent).ShouldBe(["octo-org"]);
  });

  // Precision 1 and rounding down come from the effective configuration of the repository: 82.47, 85.19 and 69.99
  // round down to 82.4, 85.1 and 69.9, where the nearest would give 82.5, 85.2 and 70.0.
  [Fact]
  public void Render_ShouldFormatAsTheConfigurationSays() => InEnglish(() =>
  {
    var cut = Render<OverviewPage>(Address());

    cut.FindAll(".cc-kpi__value").Select(static v => v.TextContent).ShouldBe(["82.4%", "85.1%", "69.9%"]);
  });

  [Fact]
  public void Render_InFrench_ShouldUseTheSeparatorsOfTheCulture()
  {
    var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      var cut = Render<OverviewPage>(Address());

      cut.FindAll(".cc-kpi__value")[0].TextContent.ShouldBe("82,4 %");
    }
    finally
    {
      (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
    }
  }

  [Fact]
  public void Render_NotFound_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetRepositoryQuery>._, A<CancellationToken>._)).ThrowsAsync(new NotFoundException(RepositoryErrors.NotFound, "Missing."));

    var cut = Render<OverviewPage>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
    cut.FindAll(".cc-kpi").ShouldBeEmpty();
    cut.FindAll(".cc-header").ShouldBeEmpty();
  }

  // A forge segment the console does not write is the same inaccessible state, and nothing is read.
  [Theory]
  [InlineData("orgs")]
  [InlineData("GitHub")]
  [InlineData("gh")]
  public void Render_UnknownForge_ShouldShowInaccessibleStateWithoutReading(string forge)
  {
    var cut = Render<OverviewPage>(Address(forge));

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Fact]
  public void Render_NoReport_ShouldShowTheNoDataState()
  {
    Overview(["backend"]);

    var cut = Render<OverviewPage>(Address());

    cut.Find("[data-state=nodata]").TextContent.ShouldContain(L("Overview_NoDataTitle"));
    A.CallTo(() => _sender.Send(A<GetCoverageHistoryQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Every flag gets its card and its change: the history of the cards (no start given) is asked 20 flags at a time.
  [Fact]
  public void Render_MoreThanTwentyFlags_ShouldAskTheChangesInChunks()
  {
    var flags = Enumerable.Range(1, 25).Select(static i => $"flag{i:00}").ToArray();
    Overview(flags, CommitFlagTotals.Create(_head, CommitFlagTotals.ProjectFlag, new CoverageTotals(1, 1, 0, 0, 0), false, null));

    var cut = Render<OverviewPage>(Address());

    cut.FindAll(".cc-kpi").Count.ShouldBe(26);
    A.CallTo(() => _sender.Send(A<GetCoverageHistoryQuery>.That.Matches(q => q.From == null && q.Flags.Count <= GetCoverageHistoryQuery.MaxFlags && q.Interval == "day" && q.Branch == "main"), A<CancellationToken>._))
     .MustHaveHappenedTwiceExactly();
    cut.FindAll(".cc-kpi__value")[1].TextContent.ShouldBe("—");
  }

  // A navigation to another repository while the first is still loading: the late answer of the first is dropped.
  [Fact]
  public void Render_FirstLoadAnsweringLast_ShouldShowTheSecond() => InEnglish(() =>
  {
    var first = new TaskCompletionSource<RepositoryOverview>();
    A.CallTo(() => _sender.Send(A<GetRepositoryQuery>.That.Matches(q => q.Name == "app"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<RepositoryOverview>(first.Task));
    var other = TrackedRepository.Create(2, "2003", RepositorySlug.Parse("octo-org/other"), "develop", false, "protected", Now);
    A.CallTo(() => _sender.Send(A<GetRepositoryQuery>.That.Matches(q => q.Name == "other"), A<CancellationToken>._))
     .Returns(new ValueTask<RepositoryOverview>(new RepositoryOverview(other, [], [], [])));
    var cut = Render<OverviewPage>(Address());

    cut.Render(static p => p.Add(static c => c.Name, "other"));
    cut.WaitForAssertion(() => cut.Find(".cc-header__subtitle").TextContent.ShouldBe(L("Repository_DefaultBranch", "develop")));
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", false, "protected", Now);
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(new RepositoryOverview(repository, [], [], [])));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find(".cc-header__subtitle").TextContent.ShouldBe(L("Repository_DefaultBranch", "develop"));
  });

  private static Action<ComponentParameterCollectionBuilder<OverviewPage>> Address(string forge = "github") =>
    p => p.Add(static c => c.Forge, forge).Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app");

  // The lower half is one composed load: the repository and its configuration are read once, for the cards.
  [Fact]
  public void Render_ShouldShowTheActivityWithoutReadingTheRepositoryAgain()
  {
    var cut = Render<OverviewPage>(Address());

    cut.WaitForAssertion(() => cut.FindAll("[data-panel=recent-commits] tbody tr").Count.ShouldBe(1));
    cut.Find("[data-panel=recent-commits] tbody tr").TextContent.ShouldContain("Add the retry policy");
    cut.Find("[data-panel=open-pulls] [data-pull='423']").TextContent.ShouldContain("feat(alerts): retry policy");
    A.CallTo(() => _sender.Send(A<GetRepositoryQuery>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<ListCommitsQuery>.That.Matches(q => q.PageSize == 5 && q.Page == 1 && q.Branch == "main"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<ListPullRequestsQuery>.That.Matches(q => q.PageSize == 5 && q.State == ListPullRequestsQuery.OpenState), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // Decision 11: a GitHub repository keeps the address of Codecov, so a README only changes host.
  [Fact]
  public void Render_PrivateRepository_ShouldWriteTheCodecovRouteWithTheToken()
  {
    Private();

    var cut = Render<OverviewPage>(Address());

    cut.WaitForAssertion(() => cut.Find("[data-panel=badge] pre.cc-code").TextContent.ShouldBe(
      "[![coverage](https://coverage-api.example.com/github/octo-org/app/graph/badge.svg?token=Kq7xT2)](https://coverage.example.com/github/octo-org/app)"));
    cut.Find("[data-panel=badge] img").GetAttribute("src")!.ShouldContain("token=Kq7xT2");
  }

  // A public repository serves its badge without a token, and the snippet carries none.
  [Fact]
  public void Render_PublicRepository_ShouldOmitTheToken()
  {
    var cut = Render<OverviewPage>(Address());

    cut.WaitForAssertion(() => cut.Find("[data-panel=badge] pre.cc-code").TextContent.ShouldNotContain("token="));
  }

  // ADR-014: only GitHub has the route of Codecov; the other forges carry their own segment.
  [Fact]
  public void Render_OtherForge_ShouldWriteTheApiRoute()
  {
    var cut = Render<OverviewPage>(p => p.Add(static c => c.Forge, "bitbucket").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app"));

    cut.WaitForAssertion(() => cut.Find("[data-panel=badge] pre.cc-code").TextContent.ShouldContain(
      "https://coverage-api.example.com/api/v1/badges/bitbucket/octo-org/app.svg"));
  }

  // Without the public addresses, the snippets are replaced by a banner.
  [Fact]
  public void Render_WithoutPublicUrls_ShouldShowTheBanner()
  {
    _urls.CurrentValue.ApiPublicUrl = null;

    var cut = Render<OverviewPage>(Address());

    cut.WaitForAssertion(() => cut.Find("[data-panel=badge] [data-state=nourls]").ShouldNotBeNull());
  }

  // A repository without any coverage shows its empty state only: there is nothing to list or to badge.
  [Fact]
  public void Render_WithoutCoverage_ShouldNotReadTheActivity()
  {
    Overview([]);

    var cut = Render<OverviewPage>(Address());

    cut.Find("[data-state=nodata]").ShouldNotBeNull();
    cut.FindAll("[data-panel=recent-commits]").ShouldBeEmpty();
    A.CallTo(() => _sender.Send(A<ListCommitsQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The project rose from 81.2 to 82.4 over the period; a flag without points has no change.
  private CoverageSeries Series(string flag) =>
    flag.Length == 0
      ? new CoverageSeries(flag, [Point(Now.AddDays(-29), 81.2m), Point(Now.AddDays(-10), 81.9m), Point(Now, 82.4m)])
      : new CoverageSeries(flag, []);

  private static CoveragePoint Point(DateTimeOffset at, decimal coverage) => new(at, HeadSha, 100, 80, 20, 0, coverage);

  private void Overview(IReadOnlyList<string> flags, params CommitFlagTotals[] totals) => Overview(flags, isPrivate: false, totals);

  private void Overview(IReadOnlyList<string> flags, bool isPrivate, params CommitFlagTotals[] totals)
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", isPrivate, "protected", Now);

    A.CallTo(() => _sender.Send(A<GetRepositoryQuery>.That.Matches(q => q.Owner == "octo-org" && q.Name == "app"), A<CancellationToken>._))
     .Returns(new ValueTask<RepositoryOverview>(new RepositoryOverview(repository, [], flags, totals)));
  }

  // The same repository, private: its badge needs the token every reader may read.
  private void Private()
  {
    Overview(
      ["backend", "frontend"],
      isPrivate: true,
      CommitFlagTotals.Create(_head, CommitFlagTotals.ProjectFlag, new CoverageTotals(8247, 1753, 0, 0, 0), false, null));
    A.CallTo(() => _sender.Send(A<GetBadgeTokenQuery>._, A<CancellationToken>._)).Returns(new ValueTask<string?>("Kq7xT2"));
  }

  // The panels of the lower half: five commits, five open pull requests, and no badge token for a public repository.
  private void Activity()
  {
    var git = GitCommit.Create(5, new ForgeCommitInfo(HeadSha, [], "Add the retry policy", "jo-rivera", Now.AddHours(-3)), Now);
    A.CallTo(() => _sender.Send(A<ListCommitsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call => new ValueTask<CommitSummaryPage>(
                      new CommitSummaryPage([new CommitSummary(_head, git, 1.2m)], 1, call.GetArgument<ListCommitsQuery>(0)!.PageSize, 1)));

    var pull = PullRequest.Create(5, new ForgePullInfo(423, ForgePullState.Open, "feat(alerts): retry policy", "jo-rivera", HeadSha, "feature/retry", "main", OriginSha), Now);
    A.CallTo(() => _sender.Send(A<ListPullRequestsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call => new ValueTask<PullRequestSummaryPage>(
                      new PullRequestSummaryPage([new PullRequestSummary(pull, null)], 1, call.GetArgument<ListPullRequestsQuery>(0)!.PageSize, 1)));
    A.CallTo(() => _sender.Send(A<GetBadgeTokenQuery>._, A<CancellationToken>._)).Returns(new ValueTask<string?>((string?)null));
  }

  private void Config(ConfigNode root) =>
    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(root), ConfigSource.Commit, HeadSha, "codecov.yml", null, [], [])));

  private void InEnglish(Action test)
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

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
