// ------------------------------------------------------------------------------------------------
// <copyright file="OpenPullRequests.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.PullRequests;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The open pull requests of a repository, on the overview, with the coverage of their patch; each links to its page.
/// </summary>
public partial class OpenPullRequests
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

  /// <summary>Gets or sets the rows to show.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<PullRequestRowViewModel> Pulls { get; set; } = [];

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private string Root => $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";

  #endregion
}
