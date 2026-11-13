// ------------------------------------------------------------------------------------------------
// <copyright file="PageProbes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Tests.Isolation;

// The probe of every routable page of the console, and how the suite finds the pages. A new page with no probe fails
// the suite: whoever adds a route says, here, what an outsider receives from it.
public static class PageProbes
{
  #region Properties

  // Every probe, by page. The ones with an address are asked for at the other organization and at an owner that never
  // existed; the ones with a reason address no organization and are only declared.
  public static IReadOnlyList<PageProbe> All { get; } =
  [
    PageProbe.NotAddressed("/access-denied", "The refusal of the console, the same for everyone."),
    PageProbe.NotAddressed("/account/tokens", "The tokens of the signed-in user, which name no organization."),
    PageProbe.NotAddressed("/admin/audit", "The audit of the instance, reserved to its administrators."),
    PageProbe.NotAddressed("/admin/operations", "The state of the instance, reserved to its administrators."),
    PageProbe.NotAddressed("/admin/organizations", "The lifecycle of the organizations, reserved to the administrators of the instance."),
    PageProbe.NotAddressed("/admin/settings", "The settings of the instance, reserved to its administrators."),
    PageProbe.Addressed("/{Forge}/{Owner}/{Name}/commit/{Sha}", $"/github/{{owner}}/app/commit/{TwoOrganizationWeb.SecondSha}"),
    PageProbe.Addressed("/{Forge}/{Owner}/{Name}/commit/{Sha}/blob/{*Path}", $"/github/{{owner}}/app/commit/{TwoOrganizationWeb.SecondSha}/blob/{TwoOrganizationWeb.SecondFile}"),
    PageProbe.Addressed("/{Forge}/{Owner}/{Name}/commits", "/github/{owner}/app/commits"),
    PageProbe.NotAddressed("/error", "The error page of the console, the same for everyone."),
    PageProbe.NotAddressed("/", "The repositories the signed-in user reads, which name no organization in the address."),
    PageProbe.NotAddressed("/not-found", "The not-found page of the console, the same for everyone."),
    PageProbe.Addressed("/orgs/{Forge}/{Login}/repositories", "/orgs/github/{owner}/repositories"),
    PageProbe.Addressed("/orgs/{Forge}/{Login}/settings", "/orgs/github/{owner}/settings"),
    PageProbe.Addressed("/orgs/{Forge}/{Login}/upload-tokens", "/orgs/github/{owner}/upload-tokens"),
    PageProbe.Addressed("/{Forge}/{Owner}/{Name}/pull/{Number:int}", $"/github/{{owner}}/app/pull/{TwoOrganizationWeb.SecondPull}"),
    PageProbe.Addressed("/{Forge}/{Owner}/{Name}/pulls", "/github/{owner}/app/pulls"),
    PageProbe.Addressed("/{Forge}/{Owner}/{Name}", "/github/{owner}/app"),
    PageProbe.Addressed("/{Forge}/{Owner}/{Name}/settings", "/github/{owner}/app/settings"),
    PageProbe.NotAddressed("/setup", "The first-start wizard, which no organization owns."),
    PageProbe.NotAddressed("/signin", "The sign-in of the console, the same for everyone."),
    PageProbe.NotAddressed("/_ui", "The gallery of the components, which holds no data."),
  ];

  #endregion

  #region Methods

  // Every routable page of the console, by its route template.
  public static IReadOnlyList<string> Pages()
  {
    var components = typeof(Web.Components.App).Assembly.GetTypes()
                                               .Where(static t => t is { IsAbstract: false } && typeof(IComponent).IsAssignableFrom(t));

    return
    [
      .. components.SelectMany(static c => c.GetCustomAttributes<RouteAttribute>().Select(static r => r.Template))
                   .Distinct(StringComparer.Ordinal)
                   .Order(StringComparer.Ordinal),
    ];
  }

  #endregion
}
