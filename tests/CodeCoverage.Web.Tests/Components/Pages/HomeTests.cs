// ------------------------------------------------------------------------------------------------
// <copyright file="HomeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Features.Access.Queries.GetCurrentUserAccess;
using CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;
using CodeCoverage.Application.Features.Repositories.Queries.ListRepositorySummaries;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The home page (M3.20) against a fake sender.
public sealed class HomeTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<ListRepositorySummariesQuery> _queries = [];

  #endregion

  #region Ctors

  public HomeTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

    User(isInstanceAdministrator: false, Member("acme-labs"), Member("blue-harbor"));
    A.CallTo(() => _sender.Send(A<ListRepositorySummariesQuery>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var query = call.GetArgument<ListRepositorySummariesQuery>(0)!;
        _queries.Add(query);

        return new ValueTask<IReadOnlyList<RepositorySummary>>(
          query.Login == "acme-labs"
            ? [Summary("billing-api", covered: true, disabled: false, "backend", "frontend"), Summary("legacy-portal", covered: false, disabled: true)]
            : [Summary("harbor-web", covered: true, disabled: false)]);
      });
  }

  #endregion

  #region Methods

  // One request per organization of the user, one panel each.
  [Fact]
  public void Render_ShouldShowOnePanelPerOrganization()
  {
    var cut = Render<Home>();

    _queries.Select(static q => q.Login).ShouldBe(["acme-labs", "blue-harbor"]);
    cut.FindAll("section[data-organization]").Select(static s => s.GetAttribute("data-organization")).ShouldBe(["acme-labs", "blue-harbor"]);
    var billing = cut.Find("tr[data-repository=billing-api]");
    billing.QuerySelector("a")!.GetAttribute("href").ShouldBe("github/acme-labs/billing-api");
    billing.QuerySelector("code")!.TextContent.ShouldBe("e4f5a6b");
    billing.QuerySelector("svg.cc-spark").ShouldNotBeNull();
    billing.TextContent.ShouldContain("backend");
    var legacy = cut.Find("tr[data-repository=legacy-portal]");
    legacy.TextContent.ShouldContain(L("Repository_Inactive"));
    legacy.QuerySelectorAll("td")[1].TextContent.ShouldBe("—");
  }

  [Fact]
  public void Search_ShouldFilterList()
  {
    var cut = Render<Home>();

    cut.Find(".cc-search input").Input("HARBOR");

    cut.FindAll("tr[data-repository]").Select(static r => r.GetAttribute("data-repository")).ShouldBe(["harbor-web"]);

    cut.Find(".cc-search input").Input("nothing-like-it");

    cut.Find("[data-state=nomatch]").TextContent.ShouldContain(L("Home_NoMatchTitle"));
  }

  [Fact]
  public void FilterInactive_ShouldKeepTheDisabledRepositories()
  {
    var cut = Render<Home>();

    cut.FindAll(".cc-header [role=tab]").Last().Click();

    cut.FindAll("tr[data-repository]").Select(static r => r.GetAttribute("data-repository")).ShouldBe(["legacy-portal"]);
  }

  [Fact]
  public void Render_Organization_ShouldNarrowToIt()
  {
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("org", "github/blue-harbor"));

    var cut = Render<Home>();

    _queries.ShouldHaveSingleItem().Login.ShouldBe("blue-harbor");
    cut.FindAll("section[data-organization]").ShouldHaveSingleItem();
  }

  // An organization the user does not belong to reads nothing and looks like no organization at all.
  [Fact]
  public void Render_ForeignOrganization_ShouldShowTheEmptyStateWithoutReading()
  {
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("org", "github/other-org"));

    var cut = Render<Home>();

    _queries.ShouldBeEmpty();
    cut.Find("[data-state=empty]").TextContent.ShouldContain(L("Home_EmptyTitle"));
  }

  [Fact]
  public void Render_Empty_ShouldShowEmptyState()
  {
    User(isInstanceAdministrator: true);

    var cut = Render<Home>();

    var empty = cut.Find("[data-state=empty]");
    empty.TextContent.ShouldContain(L("Home_EmptyInstanceAdmin"));
    empty.QuerySelector("a")!.GetAttribute("href").ShouldBe("admin/organizations");
  }

  // The coverage of the head is drawn as a bar, its value kept as text; a repository without coverage has no bar.
  [Fact]
  public void Render_ShouldDrawTheCoverageBar()
  {
    var cut = Render<Home>();

    var bar = cut.Find("tr[data-repository=billing-api] .cc-covbar");
    bar.QuerySelector(".cc-covbar__track")!.GetAttribute("aria-hidden").ShouldBe("true");
    bar.QuerySelector(".cc-covbar__h")!.GetAttribute("style").ShouldBe("width:82.41%");
    bar.QuerySelector(".cc-covbar__m")!.GetAttribute("style").ShouldBe("width:17.59%");
    bar.TextContent.Trim().ShouldEndWith("%");
    cut.Find("tr[data-repository=legacy-portal]").QuerySelector(".cc-covbar__track").ShouldBeNull();
  }

  // Without a search or a filter, an organization without readable repository keeps its panel, its count and an empty body.
  [Fact]
  public void Render_OrganizationWithoutRepository_ShouldKeepItsPanel()
  {
    A.CallTo(() => _sender.Send(A<ListRepositorySummariesQuery>.That.Matches(q => q.Login == "blue-harbor"), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<RepositorySummary>>([]));

    var cut = Render<Home>();

    var panel = cut.Find("section[data-organization=blue-harbor]");
    panel.QuerySelector(".cc-badge")!.TextContent.ShouldContain("0");
    panel.QuerySelector("[data-state=empty]")!.TextContent.ShouldContain(L("Home_EmptyTitle"));

    cut.Find(".cc-search input").Input("billing");

    cut.FindAll("section[data-organization]").Select(static s => s.GetAttribute("data-organization")).ShouldBe(["acme-labs"]);
  }

  // A narrowing to one organization while the first load is pending: the late answer of the first is dropped.
  [Fact]
  public void Render_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<IReadOnlyList<RepositorySummary>>();
    A.CallTo(() => _sender.Send(A<ListRepositorySummariesQuery>.That.Matches(q => q.Login == "blue-harbor"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<RepositorySummary>>(first.Task));
    var cut = Render<Home>();

    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("org", "github/acme-labs"));
    cut.WaitForAssertion(() => cut.FindAll("section[data-organization]").ShouldHaveSingleItem());
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult([Summary("harbor-web", covered: true, disabled: false)]));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.FindAll("section[data-organization]").Select(static s => s.GetAttribute("data-organization")).ShouldBe(["acme-labs"]);
  }

  private static MemberOrganization Member(string login)
  {
    var organization = Organization.CreatePending(Forge.GitHub, login.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), login, OrganizationAccountType.Organization, Now);
    organization.Admit("octocat", Now);

    return new MemberOrganization(organization, OrganizationRole.Participant);
  }

  private static RepositorySummary Summary(string name, bool covered, bool disabled, params string[] flags)
  {
    var repository = TrackedRepository.Create(1, name, RepositorySlug.Parse($"acme-labs/{name}"), "main", false, "protected", Now);

    if (disabled)
    {
      repository.Disable(Now);
    }

    Commit? head = null;

    if (covered)
    {
      head = Commit.Create(5, GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7)), Now.AddHours(-2));
      head.MarkPending("main", null, Now.AddHours(-2));
      head.RecordFinalization(CommitState.Complete, 1, new CoverageTotals(8241, 1759, 0, 0, 0), 1, 1, CarryforwardStatus.None, "default", Now.AddHours(-2));
    }

    return new RepositorySummary(repository, head, flags, covered ? [new DailyCoverage(Now.AddDays(-2), 80m), new DailyCoverage(Now.AddDays(-1), 82.41m)] : []);
  }

  private void User(bool isInstanceAdministrator, params MemberOrganization[] organizations)
  {
    A.CallTo(() => _sender.Send(A<GetCurrentUserAccessQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<UserAccess>(new UserAccess(AppUser.Create(Forge.GitHub, "42", "octocat", null), isInstanceAdministrator)));
    A.CallTo(() => _sender.Send(A<ListMyOrganizationsQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<MemberOrganization>>(organizations));
  }

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
