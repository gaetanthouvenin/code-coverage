// ------------------------------------------------------------------------------------------------
// <copyright file="CommitStateBadge.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>
///   The state of a commit with the three labels of the mockup: known and pending show as processing, and a compacted
///   commit shows as complete with a hint, its totals being kept.
/// </summary>
public partial class CommitStateBadge
{
  #region Parameters

  /// <summary>Gets or sets the state.</summary>
  [Parameter, EditorRequired]
  public CommitStatus Status { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
