// ------------------------------------------------------------------------------------------------
// <copyright file="PageProbe.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Tests.Isolation;

// What a page is probed with: the address to ask for at the organization the user knows nothing about, or the reason
// why the page addresses no organization. A page with neither fails EveryPage_ShouldDeclareProbe.
public sealed record PageProbe
{
  #region Ctors

  private PageProbe(string route, string? address, string? reason)
  {
    Route = route;
    Address = address;
    Reason = reason;
  }

  #endregion

  #region Properties

  // The route template of the page, as its @page directive writes it.
  public string Route { get; }

  // The address asked for; {owner} stands for the owner the suite substitutes.
  public string? Address { get; }

  // Why the page addresses no organization.
  public string? Reason { get; }

  public bool IsCrossOrganization => Address is not null;

  #endregion

  #region Methods

  public static PageProbe Addressed(string route, string address) => new(route, address, null);

  public static PageProbe NotAddressed(string route, string reason) => new(route, null, reason);

  public override string ToString() => Route;

  #endregion
}
