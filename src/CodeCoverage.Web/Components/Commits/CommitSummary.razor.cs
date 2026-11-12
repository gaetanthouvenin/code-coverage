// ------------------------------------------------------------------------------------------------
// <copyright file="CommitSummary.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>The totals of a commit in cards: coverage against its base, files, lines and sessions.</summary>
public partial class CommitSummary
{
  #region Parameters

  /// <summary>Gets or sets the commit.</summary>
  [Parameter, EditorRequired]
  public CommitDetailViewModel Commit { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
