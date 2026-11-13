// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRouteTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CodeCoverage.Api.Tests.Isolation;

// ADR-014: an address that names an organization names its forge first, so two accounts of the same login on two
// forges are never the same owner. The rule is read on the routes themselves, without a host.
public sealed class ForgeRouteTests
{
  #region Fields

  // The compatibility routes of Codecov carry the forge as the service segment ({service}: gh or github).
  private const string Service = "{service";

  private const string Forge = "{forge}";

  #endregion

  #region Methods

  [Fact]
  public void EveryOrganizationRoute_ShouldCarryTheForge()
  {
    var routes = OrganizationRoutes();

    routes.ShouldNotBeEmpty();
    routes.Where(static r => !r.Template.Contains(Forge, StringComparison.Ordinal) && !r.Template.Contains(Service, StringComparison.Ordinal))
          .Select(static r => $"{r.Action}: {r.Template}")
          .ShouldBeEmpty("a route that names an owner or an organization names its forge first (ADR-014)");
  }

  // Every route that addresses an owner or an organization, with the action it belongs to.
  private static IReadOnlyList<(string Action, string Template)> OrganizationRoutes()
  {
    var controllers = typeof(CodeCoverageApi).Assembly.GetTypes()
                                             .Where(static t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t));

    return
    [
      .. from controller in controllers
         let prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty
         from method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
         from attribute in method.GetCustomAttributes<HttpMethodAttribute>()
         let template = Join(prefix, attribute.Template)
         where template.Contains("{owner}", StringComparison.Ordinal) || template.Contains("{org}", StringComparison.Ordinal)
         select ($"{controller.Name}.{method.Name}", template),
    ];
  }

  private static string Join(string prefix, string? template) =>
    template is null ? prefix : template.StartsWith('/') || prefix.Length == 0 ? template : $"{prefix}/{template}";

  #endregion
}
