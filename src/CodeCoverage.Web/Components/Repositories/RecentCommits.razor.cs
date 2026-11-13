// ------------------------------------------------------------------------------------------------
// <copyright file="RecentCommits.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>The latest commits of the default branch, on the overview, each linking to its own page.</summary>
public partial class RecentCommits
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
  public IReadOnlyList<CommitRowViewModel> Commits { get; set; } = [];

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private string Root => $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";

  #endregion

  #region Methods

  private static string DeltaClass(int trend) => trend switch
  {
    > 0 => "cc-delta--up",
    < 0 => "cc-delta--down",
    var _ => "cc-dim",
  };

  #endregion
}
