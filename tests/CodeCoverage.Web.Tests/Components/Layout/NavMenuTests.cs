// ------------------------------------------------------------------------------------------------
// <copyright file="NavMenuTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Layout;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Access;

namespace CodeCoverage.Web.Tests.Components.Layout;

// The entries of the top bar follow what the user administers (validated mockup, #navigation).
public sealed class NavMenuTests : UiTestContext
{
  #region Fields

  private static readonly NavigationOrganizationViewModel Participant = new("github", "GitHub", "octo-org", false);

  private static readonly NavigationOrganizationViewModel Administered = new("github", "GitHub", "acme", true);

  #endregion

  #region Methods

  [Fact]
  public void Render_NonAdmin_ShouldHideAdministration()
  {
    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [Participant])));

    Tabs(cut).ShouldBe([Common("Nav_Repositories"), Common("Nav_Account")]);
    cut.Find(".cc-user-menu__role").TextContent.ShouldBe(Common("Role_Participant"));
  }

  [Fact]
  public void Render_OrganizationAdmin_ShouldShowOrganizationSection()
  {
    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [Administered, Participant])));

    Tabs(cut).ShouldBe([Common("Nav_Repositories"), Common("Nav_Organization"), Common("Nav_Account")]);
    cut.Find(".cc-topnav__tabs a[href='orgs/github/acme/repositories']").ShouldNotBeNull();
    cut.Find(".cc-user-menu__role").TextContent.ShouldBe(Common("Role_OrganizationAdmin"));
  }

  [Fact]
  public void Render_InstanceAdmin_ShouldShowInstanceSection()
  {
    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", true, [])));

    Tabs(cut).ShouldBe([Common("Nav_Repositories"), Common("Nav_Instance"), Common("Nav_Account")]);
    cut.Find(".cc-topnav__tabs a[href='admin/organizations']").ShouldNotBeNull();
    cut.Find(".cc-user-menu__role").TextContent.ShouldBe(Common("Role_InstanceAdmin"));
  }

  [Fact]
  public void Render_SignedOut_ShouldKeepTheRepositoriesOnly()
  {
    var cut = Render<NavMenu>();

    Tabs(cut).ShouldBe([Common("Nav_Repositories")]);
    cut.FindAll(".cc-user-menu").ShouldBeEmpty();
    cut.FindAll(".cc-topnav__org").ShouldBeEmpty();
  }

  // The switcher: hidden without organization, « All organizations » on the home page only, a login of two forges
  // told apart by its forge.
  [Fact]
  public void Render_Switcher_ShouldListTheOrganizations()
  {
    var gitLab = new NavigationOrganizationViewModel("gitlab", "GitLab", "acme", false);

    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [Administered, gitLab, Participant])));

    var options = cut.FindAll(".cc-topnav__bar .cc-topnav__org option").Select(static o => (o.GetAttribute("value"), o.TextContent)).ToList();
    options.ShouldBe(
    [
      (string.Empty, Common("Org_All")),
      ("github/acme", $"acme ({Common("Forge_GitHub")})"),
      ("gitlab/acme", $"acme ({Common("Forge_GitLab")})"),
      ("github/octo-org", "octo-org"),
    ]);
  }

  [Fact]
  public void Render_OnARepository_ShouldSelectItsOrganizationAndMarkTheRepositories()
  {
    Navigation.NavigateTo("github/octo-org/app/commits");

    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [Administered, Participant])));

    cut.FindAll(".cc-topnav__bar .cc-topnav__org option").Select(static o => o.GetAttribute("value")).ShouldNotContain(string.Empty);
    cut.Find(".cc-topnav__bar .cc-topnav__org option[selected]").GetAttribute("value").ShouldBe("github/octo-org");
    cut.Find(".cc-topnav__tabs .cc-topnav__tab--active").TextContent.ShouldBe(Common("Nav_Repositories"));
    cut.Find(".cc-user-menu__role").TextContent.ShouldBe(Common("Role_Participant"));
  }

  // Review R6: with no current organization, a neutral choice is selected instead of the first organization.
  [Theory]
  [InlineData("account/tokens")]
  [InlineData("admin/settings")]
  [InlineData("github/someone-else/app")]
  public void Render_NoCurrentOrganization_ShouldSelectANeutralChoice(string path)
  {
    Navigation.NavigateTo(path);

    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [Administered, Participant])));

    var first = cut.FindAll(".cc-topnav__bar .cc-topnav__org option")[0];
    first.GetAttribute("value").ShouldBe(string.Empty);
    first.TextContent.ShouldBe(Common("Org_Choose"));
    first.HasAttribute("selected").ShouldBeTrue();
  }

  // Review R11: a forge alias of ADR-014 (gh, gl, bb) names the same organization.
  [Fact]
  public void Render_OnARepositoryUnderAnAlias_ShouldSelectItsOrganization()
  {
    Navigation.NavigateTo("gh/octo-org/app");

    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [Administered, Participant])));

    cut.Find(".cc-topnav__bar .cc-topnav__org option[selected]").GetAttribute("value").ShouldBe("github/octo-org");
  }

  // Decision 17: the organization filter of the home page is ?org={forge}/{login}.
  [Fact]
  public void ChangeOrganization_OnTheHomePage_ShouldFilterIt()
  {
    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [Administered, Participant])));

    cut.Find(".cc-topnav__bar .cc-topnav__org select").Change("github/octo-org");

    Navigation.Uri.ShouldEndWith("?org=github%2Focto-org");
  }

  [Fact]
  public void UserMenu_ShouldOfferTheAccountAndAProtectedSignOut()
  {
    var cut = Render<NavMenu>(p => p.Add(n => n.Model, new NavigationViewModel("octocat", false, [])));

    cut.Find(".cc-user-menu__trigger").Click();

    cut.Find(".cc-user-menu__panel a[href='account/tokens']").TextContent.ShouldBe(Common("Nav_Account"));
    var form = cut.Find(".cc-user-menu__panel form");
    form.GetAttribute("method").ShouldBe("post");
    form.GetAttribute("action").ShouldBe("signout");
  }

  [Theory]
  [InlineData("github/octo-org/app", "github", "octo-org")]
  [InlineData("gl/group:sub/app/pulls", "gitlab", "group")]
  [InlineData("gh/octo-org/app", "github", "octo-org")]
  [InlineData("orgs/gh/acme/settings", "github", "acme")]
  [InlineData("orgs/github/acme/settings", "github", "acme")]
  [InlineData("?org=github%2Facme", "github", "acme")]
  public void OrganizationOf_Address_ShouldNameItsOrganization(string path, string forge, string login) =>
    NavMenu.OrganizationOf(path).ShouldBe((forge, login));

  [Theory]
  [InlineData("")]
  [InlineData("admin/settings")]
  [InlineData("account/tokens")]
  [InlineData("svn/octo-org/app")]
  public void OrganizationOf_OtherAddress_ShouldNameNone(string path) => NavMenu.OrganizationOf(path).ShouldBeNull();

  private static List<string> Tabs(IRenderedComponent<NavMenu> cut) => [.. cut.FindAll(".cc-topnav__tabs a").Select(static a => a.TextContent)];

  private string Common(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
