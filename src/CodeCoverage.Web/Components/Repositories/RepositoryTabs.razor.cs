// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTabs.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>The sections of a repository: overview, commits and pull requests, in pill tabs.</summary>
/// <remarks>
///   The settings of the repository get their tab with the permission of its administrators (M3.34); until then the tab
///   is not shown, which reveals nothing.
/// </remarks>
public partial class RepositoryTabs
{
  #region Parameters

  /// <summary>Gets or sets the forge segment of the address of the repository, e.g. <c>github</c>.</summary>
  [Parameter, EditorRequired]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner segment of the address of the repository.</summary>
  [Parameter, EditorRequired]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter, EditorRequired]
  public string Name { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private IReadOnlyList<CcNavItem> Items { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    var root = $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";

    Items =
    [
      new CcNavItem(root, L["Tab_Overview"], CcIconName.Chart, NavLinkMatch.All),
      new CcNavItem($"{root}/commits", L["Tab_Commits"], CcIconName.Commit),
      new CcNavItem($"{root}/pulls", L["Tab_Pulls"], CcIconName.PullRequest),
    ];
  }

  #endregion
}
