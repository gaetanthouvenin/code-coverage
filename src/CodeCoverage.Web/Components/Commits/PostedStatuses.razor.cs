// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatuses.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>The statuses posted on the forge for a commit, each with its state in text and icon.</summary>
public partial class PostedStatuses
{
  #region Parameters

  /// <summary>Gets or sets the statuses.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<PostedStatusViewModel> Statuses { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
