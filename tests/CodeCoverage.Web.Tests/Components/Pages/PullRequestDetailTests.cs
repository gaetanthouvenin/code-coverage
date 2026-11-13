// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestDetailTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.PullRequests;
using CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequest;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

using PullRequestDetailPage = CodeCoverage.Web.Components.Pages.PullRequestDetail;

namespace CodeCoverage.Web.Tests.Components.Pages;

public sealed class PullRequestDetailTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7));

  private static readonly GitSha ForkPoint = GitSha.Parse("a1b2c3d" + new string('2', GitSha.Length - 7));

  private static readonly GitSha ComparedTo = GitSha.Parse("9f8e7d6" + new string('3', GitSha.Length - 7));

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public PullRequestDetailTests()
  {
    Services.AddFakeSenderRunner(_sender);

    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Builtin, null, null, null, [], [])));
  }

  #endregion

  #region Methods

  // The base report was found before the fork point and the base branch moved on: the page says both, as the comment.
  [Fact]
  public void Render_ComparedToDiffersFromForkPoint_ShouldExplain() => InEnglish(() =>
  {
    Returns(Detail(Comparison(ComparedTo, behindBy: 3, hops: 2)));

    var cut = Render<PullRequestDetailPage>(Address(423));

    cut.Find(".cc-header__title").TextContent.ShouldContain("#423 feat(alerts): retry policy");
    var subtitle = cut.Find(".cc-header__subtitle");
    subtitle.TextContent.ShouldContain("jo-rivera");
    subtitle.TextContent.ShouldContain("feature/webhook-retry");
    var link = subtitle.QuerySelector("a[data-link=comment]")!;
    link.GetAttribute("href").ShouldBe("https://github.com/octo-org/app/pull/423#issuecomment-987");
    link.GetAttribute("rel").ShouldBe("noopener noreferrer");
    cut.Find("[data-base=head] code").TextContent.ShouldBe("e4f5a6b");
    cut.Find("[data-base=fork]").TextContent.ShouldContain(L("Comparison_OnBranch", "a1b2c3d", "main"));
    cut.Find("[data-base=compared] code").TextContent.ShouldBe("9f8e7d6");
    cut.Find("[data-base=hops]").TextContent.ShouldBe(L("Comparison_Hops", 2));
    cut.Find("[data-base=behind]").TextContent.ShouldBe(L("Comparison_BehindBy", "main", 3));
    cut.Find(".cc-pill-tabs__tab--active").TextContent.Trim().ShouldBe(L("Tab_Pulls"));
  });

  // A comparison stored before M3.31 knows neither distance: no « 0 », no Behind row, no hops.
  [Fact]
  public void Render_OldComparison_ShouldShowNoDistance()
  {
    Returns(Detail(Comparison(ForkPoint, behindBy: null, hops: null)));

    var cut = Render<PullRequestDetailPage>(Address(423));

    cut.FindAll("[data-base=behind]").ShouldBeEmpty();
    cut.FindAll("[data-base=hops]").ShouldBeEmpty();
  }

  // The diff table has the rows and signs of the comment: fewer misses improve the project, so the row starts with « + ».
  [Fact]
  public void Render_ShouldDrawTheDiffTableOfTheComment()
  {
    Returns(Detail(Comparison(ComparedTo, behindBy: 0, hops: 0)));

    var cut = Render<PullRequestDetailPage>(Address(423));

    var lines = cut.FindAll("[data-panel=diff] pre span").Select(static s => (s.TextContent, s.ClassName)).ToList();
    lines[0].TextContent.ShouldContain("Coverage Diff");
    lines.ShouldContain(l => l.TextContent.StartsWith("+ Coverage", StringComparison.Ordinal) && l.ClassName == "cc-code__add");
    lines.ShouldContain(l => l.TextContent.StartsWith("  Files", StringComparison.Ordinal) && l.TextContent.Contains("210") && l.TextContent.Contains("212"));
    lines.ShouldContain(l => l.TextContent.StartsWith("+ Misses", StringComparison.Ordinal) && l.ClassName == "cc-code__add");
    cut.FindAll("[data-base=behind]").ShouldBeEmpty();
  }

  // Decision D4: a readable pull request whose head has no comparison yet keeps its header and says it is pending.
  [Fact]
  public void Render_ComparisonPending_ShouldShowPendingState()
  {
    Returns(Detail(null));

    var cut = Render<PullRequestDetailPage>(Address(423));

    cut.Find(".cc-header__title").TextContent.ShouldContain("feat(alerts): retry policy");
    cut.Find("[data-state=pending]").TextContent.ShouldContain(L("Pull_ComparisonPending"));
    cut.FindAll("[data-state=missing]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_NotFound_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetPullRequestQuery>._, A<CancellationToken>._)).Throws(new NotFoundException(PullRequestErrors.NotFound, "none"));

    var cut = Render<PullRequestDetailPage>(Address(9));

    cut.Find("[data-state=missing]").ShouldNotBeNull();
    cut.FindAll("[data-state=pending]").ShouldBeEmpty();
  }

  // V3: another pull request whose read fails never shows the previous one, its title nor its Re-notify.
  [Fact]
  public void Render_NextPullFailing_ShouldForgetThePrevious()
  {
    A.CallTo(() => _sender.Send(A<GetPullRequestQuery>.That.Matches(static q => q.Number == 7), A<CancellationToken>._))
     .Returns(new ValueTask<PullRequestDetail>(Detail(Comparison(ComparedTo, 1, 1), number: 7, title: "seventh")));
    A.CallTo(() => _sender.Send(A<GetPullRequestQuery>.That.Matches(static q => q.Number == 8), A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "down"));
    A.CallTo(() => _sender.Send(A<Application.Features.Repositories.Queries.GetRepositoryPermission.GetRepositoryPermissionQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<bool>(true));
    var cut = Render<PullRequestDetailPage>(Address(7));
    cut.WaitForAssertion(() => cut.Find(".cc-header__title").TextContent.ShouldContain("seventh"));

    cut.Render(static p => p.Add(static c => c.Number, 8));

    cut.WaitForAssertion(() => cut.Find(".cc-status-page").TextContent.ShouldContain(L("Error_Title")));
    cut.Markup.ShouldNotContain("seventh");
    cut.Find(".cc-header__title").TextContent.Trim().ShouldBe("#8");
    cut.FindAll(".cc-action-renotify").ShouldBeEmpty();
    cut.FindAll("a[data-link=comment]").ShouldBeEmpty();
  }

  // V5: without a fork point the hops count from the first parent, not from a merge base: no « before the merge base ».
  [Fact]
  public void Render_NoForkPoint_ShouldNotCountHopsFromAMergeBase()
  {
    var view = Comparison(ComparedTo, behindBy: null, hops: 2);
    Returns(Detail(view with { Comparison = view.Comparison with { ForkPointSha = null } }));

    var cut = Render<PullRequestDetailPage>(Address(423));

    cut.Find("[data-base=compared] code").TextContent.ShouldBe("9f8e7d6");
    cut.FindAll("[data-base=hops]").ShouldBeEmpty();
  }

  // V6: the file with the most missed lines is written as each language writes « name: count ».
  [Theory]
  [InlineData("en-US", "Send.cs: 9")]
  [InlineData("fr-FR", "Send.cs : 9")]
  public void Render_WorstFile_ShouldFollowTheLanguage(string culture, string expected)
  {
    Returns(Detail(Comparison(ComparedTo, 1, 1)));
    var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    try
    {
      var cut = Render<PullRequestDetailPage>(Address(423));

      cut.FindAll("[data-panel=comparison-kpis] .cc-kpi")[3].TextContent.ShouldContain(expected);
    }
    finally
    {
      (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
    }
  }

  // The comment link is built for GitHub only: another forge with a stored comment gets none.
  [Fact]
  public void Render_OtherForgeWithComment_ShouldShowNoLink()
  {
    Returns(Detail(Comparison(ComparedTo, 1, 1), commentId: 987));

    var cut = Render<PullRequestDetailPage>(p => p.Add(static c => c.Forge, "gitlab").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app").Add(static c => c.Number, 423));

    cut.Find(".cc-header__title").TextContent.ShouldContain("feat(alerts): retry policy");
    cut.FindAll("a[data-link=comment]").ShouldBeEmpty();
  }

  // No comment posted yet: no link, and no forge is ever asked for one.
  [Fact]
  public void Render_NoComment_ShouldShowNoLink()
  {
    var detail = Detail(Comparison(ComparedTo, 1, 1), commentId: null);
    Returns(detail);

    var cut = Render<PullRequestDetailPage>(Address(423));

    cut.FindAll("a[data-link=comment]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_BaseMissing_ShouldShowNoBaseReport()
  {
    var comparison = Comparison(ComparedTo, 1, 1);
    Returns(Detail(comparison with { Comparison = comparison.Comparison with { Result = comparison.Comparison.Result with { Base = null } }, BaseFiles = null }));

    var cut = Render<PullRequestDetailPage>(Address(423));

    cut.Find("[data-panel=comparison-kpis] .cc-kpi").TextContent.ShouldContain(L("Comparison_NoBaseReport"));
    cut.Find("[data-base=compared]").TextContent.ShouldContain(L("Comparison_NoBaseReport"));
    cut.Find("[data-base=expected]").TextContent.ShouldBe(L("Comparison_UploadBase", "a1b2c3d"));
  }

  // The first pull request answering after the second never replaces it.
  [Fact]
  public void Render_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<PullRequestDetail>();
    A.CallTo(() => _sender.Send(A<GetPullRequestQuery>.That.Matches(static q => q.Number == 423), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<PullRequestDetail>(first.Task));
    A.CallTo(() => _sender.Send(A<GetPullRequestQuery>.That.Matches(static q => q.Number == 424), A<CancellationToken>._))
     .Returns(new ValueTask<PullRequestDetail>(Detail(null, number: 424, title: "second")));
    var cut = Render<PullRequestDetailPage>(Address(423));

    cut.Render(static p => p.Add(static c => c.Number, 424));
    cut.WaitForAssertion(() => cut.Find(".cc-header__title").TextContent.ShouldContain("second"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(Detail(null)));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find(".cc-header__title").TextContent.ShouldContain("#424 second");
  }

  private static PullRequestDetail Detail(ComparisonView? comparison, long? commentId = 987, int number = 423, string title = "feat(alerts): retry policy")
  {
    var pull = PullRequest.Create(5, new ForgePullInfo(number, ForgePullState.Open, title, "jo-rivera", Head, "feature/webhook-retry", "main", ForkPoint), Now);
    typeof(PullRequest).GetProperty(nameof(PullRequest.ForkPointSha))!.SetValue(pull, ForkPoint);

    if (commentId is { } id)
    {
      typeof(PullRequest).GetProperty(nameof(PullRequest.CommentId))!.SetValue(pull, id);
    }

    return new PullRequestDetail(pull, comparison);
  }

  private static ComparisonView Comparison(GitSha comparedTo, int? behindBy, int? hops)
  {
    var result = new Comparison(
      new CoverageTotals(8123, 1877, 0, 0, 0),
      new CoverageTotals(8000, 2000, 0, 0, 0),
      new PatchCoverage(new CoverageTotals(29, 11, 0, 0, 0), [new PatchFileCoverage("src/Alerts/Send.cs", new CoverageTotals(20, 9, 0, 0, 0), false)]),
      [],
      [],
      [],
      DiffIncomplete: false);
    var stored = new CommitComparison(9, 423, ForkPoint, comparedTo, 1, 1, DiffSource.PullFiles, result, Now, behindBy, hops);

    return new ComparisonView(Head, stored, null, false, 212, 210);
  }

  private static Action<ComponentParameterCollectionBuilder<PullRequestDetailPage>> Address(int number) =>
    p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app").Add(static c => c.Number, number);

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

  private void Returns(PullRequestDetail detail) =>
    A.CallTo(() => _sender.Send(A<GetPullRequestQuery>._, A<CancellationToken>._)).Returns(new ValueTask<PullRequestDetail>(detail));

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
