// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryHeader.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The header of every page of a repository: the crumbs (owner, repository, then the page), the title and the tabs of
///   the repository.
/// </summary>
public partial class RepositoryHeader
{
  #region Parameters

  /// <summary>Gets or sets the forge segment of the address.</summary>
  [Parameter, EditorRequired]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner of the repository.</summary>
  [Parameter, EditorRequired]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter, EditorRequired]
  public string Name { get; set; } = null!;

  /// <summary>Gets or sets the title, or <see langword="null" /> for the name of the repository.</summary>
  [Parameter]
  public string? Title { get; set; }

  /// <summary>Gets or sets the line under the title.</summary>
  [Parameter]
  public string? Subtitle { get; set; }

  /// <summary>Gets or sets the crumbs after the repository, the last one being the page; none on the overview.</summary>
  [Parameter]
  public IReadOnlyList<CcBreadcrumbItem>? Crumbs { get; set; }

  /// <summary>Gets or sets the actions on the right of the title.</summary>
  [Parameter]
  public RenderFragment? Actions { get; set; }

  #endregion

  #region Properties

  private IReadOnlyList<CcBreadcrumbItem> Breadcrumb { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    var root = $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";

    // The owner leads to the repositories of its organization on the home page (?org={forge}/{login}), the repository
    // to its overview.
    Breadcrumb = [new CcBreadcrumbItem(Owner, $"./?org={Uri.EscapeDataString($"{Forge}/{Owner}")}"), new CcBreadcrumbItem(Name, root), .. Crumbs ?? []];
  }

  #endregion
}
