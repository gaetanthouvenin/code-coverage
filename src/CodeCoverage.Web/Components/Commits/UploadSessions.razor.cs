// ------------------------------------------------------------------------------------------------
// <copyright file="UploadSessions.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>The uploads of a commit and the sessions carried forward to it, with the processing log of each upload.</summary>
public partial class UploadSessions
{
  #region Parameters

  /// <summary>Gets or sets the rows: the uploads by sequence, then the carried sessions by number.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<UploadSessionRowViewModel> Rows { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
