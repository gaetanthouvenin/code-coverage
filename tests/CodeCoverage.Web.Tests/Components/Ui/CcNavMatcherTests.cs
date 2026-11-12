// ------------------------------------------------------------------------------------------------
// <copyright file="CcNavMatcherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components.Routing;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcNavMatcherTests : UiTestContext
{
  #region Methods

  [Theory]
  [InlineData("admin/settings", "admin/settings", NavLinkMatch.Prefix, true)]
  [InlineData("admin/settings/", "/admin/settings", NavLinkMatch.Prefix, true)]
  [InlineData("admin/settings?tab=github#secrets", "admin/settings", NavLinkMatch.Prefix, true)]
  [InlineData("org/repo/commits", "org/repo", NavLinkMatch.Prefix, true)]
  [InlineData("org/repo/commits", "org/repo", NavLinkMatch.All, false)]
  [InlineData("org/repository", "org/repo", NavLinkMatch.Prefix, false)]
  [InlineData("ADMIN/Settings", "admin/settings", NavLinkMatch.All, true)]
  [InlineData("", "", NavLinkMatch.Prefix, true)]
  [InlineData("org/repo", "", NavLinkMatch.Prefix, false)]
  public void IsActive_WithAddress_ShouldFollowTheNavLinkRule(string current, string href, NavLinkMatch match, bool expected)
  {
    Navigation.NavigateTo(current);

    CcNavMatcher.IsActive(Navigation, new CcNavItem(href, "Label", Match: match)).ShouldBe(expected);
  }

  [Fact]
  public void IsActive_WithNullArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => CcNavMatcher.IsActive(null!, new CcNavItem("a", "A")));
    Should.Throw<ArgumentNullException>(() => CcNavMatcher.IsActive(Navigation, null!));
  }

  // An entry that covers several addresses says itself whether it is active.
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void IsActive_WithAnExplicitState_ShouldFollowIt(bool active)
  {
    Navigation.NavigateTo("somewhere/else");

    CcNavMatcher.IsActive(Navigation, new CcNavItem("other", "Other", Active: active)).ShouldBe(active);
  }

  #endregion
}
