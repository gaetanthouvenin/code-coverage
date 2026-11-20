// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeSnippetsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Web.Components.Repositories;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Tests.Components.Repositories;

// The badge panel of the overview (M3.23).
public sealed class BadgeSnippetsTests : UiTestContext
{
  #region Fields

  private const string Image = "https://coverage-api.example.com/github/octo-org/app/graph/badge.svg?token=Kq7xT2";

  private const string Page = "https://coverage.example.com/github/octo-org/app";

  #endregion

  #region Methods

  // A private repository carries its token in the snippet, which every reader of the repository may read.
  [Fact]
  public void Render_PrivateRepo_ShouldIncludeToken()
  {
    var cut = Render<BadgeSnippets>(p => p.Add(static c => c.Badge, Snippets()).Add(static c => c.IsPrivate, true));

    cut.Find("[data-panel=badge] pre.cc-code").TextContent.ShouldBe($"[![coverage]({Image})]({Page})");
    cut.Find("[data-panel=badge] img").GetAttribute("src").ShouldBe(Image);
    cut.Find("[data-panel=badge]").TextContent.ShouldContain(L("Overview_BadgePrivate"));
  }

  [Fact]
  public void Render_ShouldSwitchToTheHtmlSnippet()
  {
    var cut = Render<BadgeSnippets>(p => p.Add(static c => c.Badge, Snippets()).Add(static c => c.IsPrivate, true));

    cut.FindAll("[data-panel=badge] [role=tab]").Single(static t => t.TextContent.Contains("HTML", StringComparison.Ordinal)).Click();

    cut.Find("[data-panel=badge] pre.cc-code").TextContent.ShouldBe($"<a href=\"{Page}\"><img src=\"{Image}\" alt=\"coverage\"></a>");
  }

  // Without the public addresses, no snippet can be written at all, and the panel says so.
  [Fact]
  public void Render_WithoutPublicUrls_ShouldShowTheBanner()
  {
    var cut = Render<BadgeSnippets>(p => p.Add(static c => c.Badge, (BadgeSnippetsViewModel?)null));

    cut.Find("[data-panel=badge] [data-state=nourls]").TextContent.ShouldContain(L("Overview_BadgeNoUrls"));
    cut.FindAll("[data-panel=badge] pre.cc-code").ShouldBeEmpty();
    cut.FindAll("[data-panel=badge] img").ShouldBeEmpty();
  }

  // M7.30: a forge without a row in the table of Codecov (Azure DevOps) gets the neutral route of the API.
  [Fact]
  public void Render_ForgeWithoutCodecovSegment_ShouldUseTheNeutralRoute()
  {
    var badge = RepositoryMapper.ToSnippets(Forge.AzureDevOps, "contoso", "app", null, "https://coverage-api.example.com/", "https://coverage.example.com");

    var cut = Render<BadgeSnippets>(p => p.Add(static c => c.Badge, badge).Add(static c => c.IsPrivate, false));

    cut.Find("[data-panel=badge] img").GetAttribute("src").ShouldBe("https://coverage-api.example.com/api/v1/badges/azure-devops/contoso/app.svg");
    cut.Find("[data-panel=badge] pre.cc-code").TextContent.ShouldContain("(https://coverage.example.com/azure-devops/contoso/app)");
  }

  // The GitHub snippet keeps the address of Codecov, token included, as before M7.30.
  [Fact]
  public void ToSnippets_GitHub_ShouldKeepTheCodecovRoute()
  {
    var badge = RepositoryMapper.ToSnippets(Forge.GitHub, "octo-org", "app", "Kq7xT2", "https://coverage-api.example.com", "https://coverage.example.com/");

    badge.ShouldNotBeNull();
    badge.ImageUrl.ShouldBe(Image);
    badge.PageUrl.ShouldBe(Page);
  }

  [Theory]
  [InlineData(null, "https://coverage.example.com")]
  [InlineData("https://coverage-api.example.com", " ")]
  public void ToSnippets_WithoutPublicUrls_ShouldGiveNone(string? api, string? web)
  {
    RepositoryMapper.ToSnippets(Forge.GitHub, "octo-org", "app", null, api, web).ShouldBeNull();
  }

  // The addresses carry the badge token: a view model that holds them never writes them down.
  [Fact]
  public void ToString_ShouldNotCarryTheAddresses() => Snippets().ToString().ShouldNotContain("Kq7xT2");

  private static BadgeSnippetsViewModel Snippets() =>
    new(Image, Page, $"[![coverage]({Image})]({Page})", $"<a href=\"{Page}\"><img src=\"{Image}\" alt=\"coverage\"></a>");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
