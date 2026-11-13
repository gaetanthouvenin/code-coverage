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

/// <summary>The sections of a repository: overview, commits, pull requests, and the settings of its administrators.</summary>
/// <remarks>
///   The settings tab is shown only when the page says the user administers the repository
///   (<c>GetRepositoryPermission</c>): a reader never sees it, which reveals nothing.
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

  /// <summary>Gets or sets a value indicating whether the settings tab is shown: only to those who administer the repository.</summary>
  [Parameter]
  public bool ShowSettings { get; set; }

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
      .. ShowSettings ? new[] { new CcNavItem($"{root}/settings", L["Tab_RepoSettings"], CcIconName.Gear) } : [],
    ];
  }

  #endregion
}
