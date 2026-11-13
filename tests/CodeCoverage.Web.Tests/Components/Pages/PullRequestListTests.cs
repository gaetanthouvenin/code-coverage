// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestListTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The pull requests of a repository (M3.30) against a fake sender.
public sealed class PullRequestListTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<ListPullRequestsQuery> _queries = [];

  private readonly IReadOnlyList<PullRequestSummary> _rows;

  #endregion

  #region Ctors

  public PullRequestListTests()
  {
    Services.AddFakeSenderRunner(_sender);

    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Builtin, null, null, null, [], [])));

    var open = PullRequest.Create(5, new ForgePullInfo(423, ForgePullState.Open, "feat(alerts): retry policy", "jo-rivera", Sha('a'), "feature/webhook-retry", "main", Sha('b')), Now);
    var merged = PullRequest.Create(5, new ForgePullInfo(418, ForgePullState.Open, "fix: typo", null, Sha('c'), "fix/typo", "main", Sha('b')), Now);
    merged.Close(merged: true, Now);
    var comparison = new Comparison(
      new CoverageTotals(81, 19, 0, 0, 0),
      new CoverageTotals(80, 20, 0, 0, 0),
      new PatchCoverage(new CoverageTotals(3, 1, 0, 0, 0), []),
      [],
      [],
      [],
      DiffIncomplete: false);

    _rows = [new PullRequestSummary(open, comparison), new PullRequestSummary(merged, null)];
    A.CallTo(() => _sender.Send(A<ListPullRequestsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListPullRequestsQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<PullRequestSummaryPage>(new PullRequestSummaryPage(_rows, query.Page, query.PageSize, 2));
      });
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldListTheOpenPullRequestsLinkedToTheirPage()
  {
    var cut = Render<PullRequestList>(Address());

    _queries.ShouldHaveSingleItem().State.ShouldBe(ListPullRequestsQuery.OpenState);
    _queries[0].PageSize.ShouldBe(25);
    var rows = cut.FindAll("tbody tr");
    rows[0].TextContent.ShouldContain("#423 feat(alerts): retry policy");
    rows[0].TextContent.ShouldContain("feature/webhook-retry → main");
    rows[0].TextContent.ShouldContain("jo-rivera");
    rows[0].TextContent.ShouldContain(L("Pull_Open"));
    rows[0].QuerySelector(".cc-delta--up").ShouldNotBeNull();
    rows[1].TextContent.ShouldContain(L("Pull_Merged"));
    rows[1].TextContent.ShouldContain(L("Commit_Pending"));
    cut.FindAll("tbody a").Select(static a => a.GetAttribute("href")).ShouldBe(["github/octo-org/app/pull/423", "github/octo-org/app/pull/418"]);
  }

  [Fact]
  public void FilterState_ShouldReload()
  {
    var cut = Render<PullRequestList>(Address());

    cut.FindAll(".cc-header [role=tab]").Single(t => t.TextContent.Contains(L("Pull_Merged"), StringComparison.Ordinal)).Click();

    _queries.Count.ShouldBe(2);
    _queries[^1].State.ShouldBe(ListPullRequestsQuery.MergedState);
    _queries[^1].Page.ShouldBe(1);
  }

  // An unknown state is the default one, and a page out of bounds is brought back: nothing invalid is sent.
  [Fact]
  public void Render_UnknownStateAndPageOutOfBounds_ShouldSendTheDefaults()
  {
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(new Dictionary<string, object?> { ["state"] = "OPEN", ["page"] = int.MaxValue }));

    Render<PullRequestList>(Address());

    _queries.ShouldHaveSingleItem().State.ShouldBe(ListPullRequestsQuery.OpenState);
    _queries[0].Page.ShouldBe(ListPullRequestsQuery.MaxPage);
  }

  [Fact]
  public void Render_NotFound_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._)).ThrowsAsync(new NotFoundException(RepositoryErrors.NotFound, "Missing."));

    var cut = Render<PullRequestList>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
  }

  [Fact]
  public void Render_NoPullRequest_ShouldShowTheEmptyState()
  {
    A.CallTo(() => _sender.Send(A<ListPullRequestsQuery>._, A<CancellationToken>._)).Returns(new ValueTask<PullRequestSummaryPage>(new PullRequestSummaryPage([], 1, 25, 0)));

    var cut = Render<PullRequestList>(Address());

    cut.Find("[data-state=empty]").TextContent.ShouldContain(L("Pulls_EmptyTitle"));
  }

  // A page past the last one (an old link, a list that shrank) shows the last real page, with its pager.
  [Fact]
  public void Render_PagePastTheLast_ShouldShowTheLastPage()
  {
    A.CallTo(() => _sender.Send(A<ListPullRequestsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListPullRequestsQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<PullRequestSummaryPage>(new PullRequestSummaryPage(query.Page <= 3 ? _rows : [], query.Page, query.PageSize, 60));
      });
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", 9));

    var cut = Render<PullRequestList>(Address());

    cut.WaitForAssertion(() => _queries[^1].Page.ShouldBe(3));
    Navigation.Uri.ShouldContain("page=3");
    Services.GetRequiredService<NavigationManager>().ShouldBeOfType<Bunit.TestDoubles.BunitNavigationManager>().History.Last(static h => h.Uri.Contains("page=3", StringComparison.Ordinal)).Options.ReplaceHistoryEntry.ShouldBeTrue();
    cut.FindAll("tbody tr").Count.ShouldBe(2);
    cut.FindAll(".cc-panel__foot").ShouldHaveSingleItem();
  }

  // The empty page past the last one is never shown, not even for one render before the redirect.
  [Fact]
  public void Render_PagePastTheLast_ShouldNeverShowTheEmptyState()
  {
    var past = new TaskCompletionSource<PullRequestSummaryPage>();
    var last = new TaskCompletionSource<PullRequestSummaryPage>();
    A.CallTo(() => _sender.Send(A<ListPullRequestsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListPullRequestsQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<PullRequestSummaryPage>(query.Page == 9 ? past.Task : last.Task);
      });
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", 9));
    var cut = Render<PullRequestList>(Address());
    var markups = new List<string>();
    cut.OnMarkupUpdated += (_, _) => markups.Add(cut.Markup);

    cut.InvokeAsync(() => past.SetResult(new PullRequestSummaryPage([], 9, 25, 60)));

    cut.WaitForAssertion(() => _queries[^1].Page.ShouldBe(3));
    markups.ShouldNotContain(static m => m.Contains("data-state=\"empty\"", StringComparison.Ordinal));
    cut.Markup.ShouldNotContain("data-state=\"empty\"");
    cut.InvokeAsync(() => last.SetResult(new PullRequestSummaryPage(_rows, 3, 25, 60)));
    cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(2));
  }

  // A change of state while the first load is pending: the late answer of the first is dropped.
  [Fact]
  public void FilterState_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<PullRequestSummaryPage>();
    A.CallTo(() => _sender.Send(A<ListPullRequestsQuery>.That.Matches(q => q.State == ListPullRequestsQuery.OpenState), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<PullRequestSummaryPage>(first.Task));
    var cut = Render<PullRequestList>(Address());

    cut.FindAll(".cc-header [role=tab]").Single(t => t.TextContent.Contains(L("Pull_Merged"), StringComparison.Ordinal)).Click();
    cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(2));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(new PullRequestSummaryPage([], 1, 25, 0)));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll("tbody tr").Count.ShouldBe(2);
  }

  private static GitSha Sha(char c) => GitSha.Parse(new string(c, GitSha.Length));

  private static Action<ComponentParameterCollectionBuilder<PullRequestList>> Address() =>
    static p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
