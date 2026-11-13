// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTabsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Repositories;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Repositories;

public sealed class RepositoryTabsTests : UiTestContext
{
  #region Methods

  // Decision 17: pill tabs. A reader never sees the settings tab, which reveals nothing of what it holds.
  [Fact]
  public void Render_ShouldGiveTheSectionsOfTheRepositoryWithoutSettings()
  {
    Navigation.NavigateTo("github/octo-org/app/pulls");

    var cut = Render<RepositoryTabs>(p => p.Add(t => t.Forge, "github").Add(t => t.Owner, "octo-org").Add(t => t.Name, "app"));

    var links = cut.FindAll(".cc-pill-tabs a");
    links.Select(static a => a.GetAttribute("href")).ShouldBe(["github/octo-org/app", "github/octo-org/app/commits", "github/octo-org/app/pulls"]);
    cut.Find(".cc-pill-tabs__tab--active").TextContent.Trim().ShouldBe(L()["Tab_Pulls"].Value);
    cut.Find("nav").GetAttribute("aria-label").ShouldBe(L()["Tabs_Repository"].Value);
  }

  // The administrators of the repository, and of its organization, get the settings tab.
  [Fact]
  public void Render_Administrator_ShouldGiveTheSettingsTabLast()
  {
    Navigation.NavigateTo("github/octo-org/app/settings");

    var cut = Render<RepositoryTabs>(p => p.Add(t => t.Forge, "github").Add(t => t.Owner, "octo-org").Add(t => t.Name, "app").Add(t => t.ShowSettings, true));

    var links = cut.FindAll(".cc-pill-tabs a");
    links.Select(static a => a.GetAttribute("href"))
         .ShouldBe(["github/octo-org/app", "github/octo-org/app/commits", "github/octo-org/app/pulls", "github/octo-org/app/settings"]);
    cut.Find(".cc-pill-tabs__tab--active").TextContent.Trim().ShouldBe(L()["Tab_RepoSettings"].Value);
  }

  [Fact]
  public void Render_OnTheOverview_ShouldMarkItOnly()
  {
    Navigation.NavigateTo("github/octo-org/app");

    var cut = Render<RepositoryTabs>(p => p.Add(t => t.Forge, "github").Add(t => t.Owner, "octo-org").Add(t => t.Name, "app"));

    cut.FindAll(".cc-pill-tabs__tab--active").ShouldHaveSingleItem().TextContent.Trim().ShouldBe(L()["Tab_Overview"].Value);
  }

  // A commit, its source and a free comparison lie under no tab's address: the page imposes the commits tab, and no
  // other one is active.
  [Theory]
  [InlineData("github/octo-org/app/commit/0123456789abcdef0123456789abcdef01234567", RepositoryTab.Commits, "Tab_Commits")]
  [InlineData("github/octo-org/app/commit/0123456789abcdef0123456789abcdef01234567/blob/src/A.cs", RepositoryTab.Commits, "Tab_Commits")]
  [InlineData("github/octo-org/app/compare/a...b", RepositoryTab.Commits, "Tab_Commits")]
  [InlineData("github/octo-org/app/pull/7", RepositoryTab.Pulls, "Tab_Pulls")]
  public void Render_ImposedTab_ShouldMarkItOnly(string address, RepositoryTab active, string label)
  {
    Navigation.NavigateTo(address);

    var cut = Render<RepositoryTabs>(p => p.Add(t => t.Forge, "github").Add(t => t.Owner, "octo-org").Add(t => t.Name, "app").Add(t => t.Active, active));

    cut.FindAll(".cc-pill-tabs__tab--active").ShouldHaveSingleItem().TextContent.Trim().ShouldBe(L()[label].Value);
  }

  // Without an imposed tab the address decides, and /commit/{sha} is not under /commits.
  [Fact]
  public void Render_CommitAddressWithoutImposedTab_ShouldMarkNone()
  {
    Navigation.NavigateTo("github/octo-org/app/commit/0123456789abcdef0123456789abcdef01234567");

    var cut = Render<RepositoryTabs>(p => p.Add(t => t.Forge, "github").Add(t => t.Owner, "octo-org").Add(t => t.Name, "app"));

    cut.FindAll(".cc-pill-tabs__tab--active").ShouldBeEmpty();
  }

  private IStringLocalizer<Common> L() => Services.GetRequiredService<IStringLocalizer<Common>>();

  #endregion
}
