// ------------------------------------------------------------------------------------------------
// <copyright file="CommitFlags.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>The flags of a commit: coverage, change against the base and origin, with the marker of a carried forward one.</summary>
public partial class CommitFlags
{
  #region Parameters

  /// <summary>Gets or sets the flags.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<CommitFlagRowViewModel> Flags { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
