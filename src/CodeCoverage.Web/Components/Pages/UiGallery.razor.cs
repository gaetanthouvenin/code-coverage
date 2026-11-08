// ------------------------------------------------------------------------------------------------
// <copyright file="UiGallery.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The <c>/_ui</c> page: every UI component in the shell, side by side in the light and the dark
///   theme, to check them against the validated mockup.
/// </summary>
/// <remarks>Served in Development only: elsewhere the page answers « not found » (404).</remarks>
public partial class UiGallery
{
  #region Fields

  private bool _available;

  private string? _search;

  #endregion

  #region Injected services

  [Inject]
  private IHostEnvironment Environment { get; set; } = null!;

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the entries of the sample top bar.</summary>
  private static IReadOnlyList<CcNavItem> NavItems { get; } =
  [
    new("_ui", "Repositories"),
    new("_ui/pulls", "Pull requests"),
    new("_ui/account", "My account"),
    new("_ui/admin", "Administration"),
  ];

  /// <summary>Gets the entries of the sample second-level tabs.</summary>
  private static IReadOnlyList<CcNavItem> SectionItems { get; } =
  [
    new("_ui", "Overview", CcIconName.Chart, NavLinkMatch.All),
    new("_ui/commits", "Commits", CcIconName.Commit),
    new("_ui/pulls", "Pull requests", CcIconName.PullRequest),
    new("_ui/settings", "Settings", CcIconName.Gear),
  ];

  /// <summary>Gets the steps of the sample breadcrumb.</summary>
  private static IReadOnlyList<CcBreadcrumbItem> Breadcrumb { get; } =
  [
    new("my-org", "_ui"),
    new("UI components"),
  ];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized()
  {
    _available = Environment.IsDevelopment();

    if (!_available)
    {
      Navigation.NotFound();
    }
  }

  #endregion
}
