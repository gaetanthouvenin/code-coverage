// ------------------------------------------------------------------------------------------------
// <copyright file="CommitListTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits.Queries.ListCommits;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepository;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

using CommitList = CodeCoverage.Web.Components.Pages.CommitList;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The commits of a branch (M3.24) against a fake sender.
public sealed class CommitListTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Complete = GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7));

  private static readonly GitSha Pending = GitSha.Parse("a1b2c3d" + new string('2', GitSha.Length - 7));

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<ListCommitsQuery> _queries = [];

  #endregion

  #region Ctors

  public CommitListTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", false, "protected", Now);
    A.CallTo(() => _sender.Send(A<GetRepositoryQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<RepositoryOverview>(new RepositoryOverview(repository, [Branch.Create(5, "develop", Complete, Now)], [], [])));
    A.CallTo(() => _sender.Send(A<GetEffectiveConfigQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<EffectiveConfig>(new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Builtin, null, null, null, [], [])));

    var complete = Commit.Create(5, Complete, Now.AddHours(-3));
    complete.MarkPending("main", null, Now.AddHours(-3));
    complete.RecordFinalization(CommitState.Complete, 2, new CoverageTotals(8241, 1759, 0, 0, 0), 2, 2, CarryforwardStatus.None, "default", Now.AddHours(-2));
    var pending = Commit.Create(5, Pending, Now.AddMinutes(-5));
    var git = GitCommit.Create(5, new ForgeCommitInfo(Complete, [], "feat(alerts): retry policy\n\nLong body.", "jo-rivera", Now.AddHours(-3)), Now);

    A.CallTo(() => _sender.Send(A<ListCommitsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListCommitsQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<CommitSummaryPage>(new CommitSummaryPage([new CommitSummary(complete, git, 1.2m), new CommitSummary(pending, null, null)], query.Page, query.PageSize, 60));
      });
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldListTheCommitsOfTheDefaultBranch() => InEnglish(() =>
  {
    var cut = Render<CommitList>(Address());

    _queries.ShouldHaveSingleItem().Branch.ShouldBe("main");
    var rows = cut.FindAll("tbody tr");
    rows.Count.ShouldBe(2);
    rows[0].QuerySelector(".cc-ellip")!.TextContent.ShouldBe("feat(alerts): retry policy");
    rows[0].QuerySelector("a.cc-ellip")!.GetAttribute("href").ShouldBe($"github/octo-org/app/commit/{Complete.Value}");
    rows[0].QuerySelector("code")!.TextContent.ShouldBe("e4f5a6b");
    rows[0].TextContent.ShouldContain("jo-rivera");
    rows[0].TextContent.ShouldContain(L("Age_Hours", 3));
    rows[0].TextContent.ShouldContain("82.41%");
    rows[0].QuerySelector(".cc-delta--up")!.TextContent.ShouldBe("+1.20");
    rows[0].TextContent.ShouldContain(L("Commit_Complete"));
    rows[1].QuerySelector(".cc-ellip")!.TextContent.ShouldBe("a1b2c3d");
    rows[1].TextContent.ShouldContain(L("Commit_Pending"));
    rows[1].TextContent.ShouldContain("—");
    cut.Find(".cc-panel__foot span").TextContent.ShouldContain("60");
  });

  [Fact]
  public void ChangeBranch_ShouldReload()
  {
    var cut = Render<CommitList>(Address());

    cut.Find("select").Change("develop");

    Navigation.Uri.ShouldContain("branch=develop");
    _queries.Count.ShouldBe(2);
    _queries[^1].Branch.ShouldBe("develop");
    _queries[^1].Page.ShouldBe(1);
  }

  [Fact]
  public void ChangePage_ShouldReadTheNextPage()
  {
    var cut = Render<CommitList>(Address());

    cut.FindAll(".cc-panel__foot button")[1].Click();
    Navigation.Uri.ShouldContain("page=2");

    _queries[^1].Page.ShouldBe(2);
  }

  // A page beyond what a list can hold is brought back within the bounds, never sent as is.
  [Theory]
  [InlineData(0, 1)]
  [InlineData(int.MaxValue, ListCommitsQuery.MaxPage)]
  public void Render_PageOutOfBounds_ShouldBeClamped(int page, int sent)
  {
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", page));

    Render<CommitList>(Address());

    _queries.ShouldHaveSingleItem().Page.ShouldBe(sent);
  }

  [Fact]
  public void Render_NotFound_ShouldShowInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetRepositoryQuery>._, A<CancellationToken>._)).ThrowsAsync(new NotFoundException(RepositoryErrors.NotFound, "Missing."));

    var cut = Render<CommitList>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
  }

  [Fact]
  public void Render_NoCommit_ShouldShowTheEmptyState()
  {
    A.CallTo(() => _sender.Send(A<ListCommitsQuery>._, A<CancellationToken>._)).Returns(new ValueTask<CommitSummaryPage>(new CommitSummaryPage([], 1, 25, 0)));

    var cut = Render<CommitList>(Address());

    cut.Find("[data-state=empty]").TextContent.ShouldContain(L("Commits_EmptyTitle"));
  }

  // A page past the last one (an old link, a list that shrank) shows the last real page, with its pager.
  [Fact]
  public void Render_PagePastTheLast_ShouldShowTheLastPage() => InEnglish(() =>
  {
    A.CallTo(() => _sender.Send(A<ListCommitsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListCommitsQuery>(0)!;
        _queries.Add(query);
        IReadOnlyList<CommitSummary> rows = query.Page <= 3 ? [new CommitSummary(Commit.Create(5, Pending, Now), null, null)] : [];

        return new ValueTask<CommitSummaryPage>(new CommitSummaryPage(rows, query.Page, query.PageSize, 60));
      });
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", 9));

    var cut = Render<CommitList>(Address());

    cut.WaitForAssertion(() => _queries[^1].Page.ShouldBe(3));
    Navigation.Uri.ShouldContain("page=3");
    Services.GetRequiredService<NavigationManager>().ShouldBeOfType<Bunit.TestDoubles.BunitNavigationManager>().History.Last(static h => h.Uri.Contains("page=3", StringComparison.Ordinal)).Options.ReplaceHistoryEntry.ShouldBeTrue();
    cut.FindAll("tbody tr").Count.ShouldBe(1);
    cut.Find(".cc-panel__foot span").TextContent.ShouldContain("51");
  });

  // The empty page past the last one is never shown, not even for one render before the redirect.
  [Fact]
  public void Render_PagePastTheLast_ShouldNeverShowTheEmptyState() => InEnglish(() =>
  {
    var past = new TaskCompletionSource<CommitSummaryPage>();
    var last = new TaskCompletionSource<CommitSummaryPage>();
    A.CallTo(() => _sender.Send(A<ListCommitsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListCommitsQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<CommitSummaryPage>(query.Page == 9 ? past.Task : last.Task);
      });
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", 9));
    var cut = Render<CommitList>(Address());
    var markups = new List<string>();
    cut.OnMarkupUpdated += (_, _) => markups.Add(cut.Markup);

    cut.InvokeAsync(() => past.SetResult(new CommitSummaryPage([], 9, 25, 60)));

    cut.WaitForAssertion(() => _queries[^1].Page.ShouldBe(3));
    markups.ShouldNotContain(static m => m.Contains("data-state=\"empty\"", StringComparison.Ordinal));
    cut.Markup.ShouldNotContain("data-state=\"empty\"");
    cut.InvokeAsync(() => last.SetResult(new CommitSummaryPage([new CommitSummary(Commit.Create(5, Pending, Now), null, null)], 3, 25, 60)));
    cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(1));
  });

  // A change of page while the first load is pending: the late answer of the first is dropped.
  [Fact]
  public void ChangePage_FirstLoadAnsweringLast_ShouldShowTheSecond() => InEnglish(() =>
  {
    var first = new TaskCompletionSource<CommitSummaryPage>();
    A.CallTo(() => _sender.Send(A<ListCommitsQuery>.That.Matches(q => q.Page == 1), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<CommitSummaryPage>(first.Task));
    var cut = Render<CommitList>(Address());

    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", 2));
    cut.WaitForAssertion(() => cut.Find(".cc-panel__foot span").TextContent.ShouldContain("26"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(new CommitSummaryPage([], 1, 25, 60)));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find(".cc-panel__foot span").TextContent.ShouldContain("26");
    cut.FindAll("tbody tr").Count.ShouldBe(2);
  });

  private static Action<ComponentParameterCollectionBuilder<CommitList>> Address() =>
    static p => p.Add(static c => c.Forge, "github").Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app");

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

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}
