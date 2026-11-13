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
  #region Fields

  // A copy refused because the same upload was already received: running it again changes nothing.
  private const string DuplicateCode = "Upload.Duplicate";

  #endregion

  #region Parameters

  /// <summary>Gets or sets the rows: the uploads by sequence, then the carried sessions by number.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<UploadSessionRowViewModel> Rows { get; set; } = null!;

  /// <summary>Gets or sets a value indicating whether a failed upload offers its retry: to the administrators of the repository only.</summary>
  [Parameter]
  public bool CanRetry { get; set; }

  /// <summary>Gets or sets the key of the row whose retry runs, or <see langword="null" />: every retry is disabled meanwhile.</summary>
  [Parameter]
  public string? RetryingKey { get; set; }

  /// <summary>Gets or sets the callback run when the retry of a failed upload is clicked.</summary>
  [Parameter]
  public EventCallback<UploadSessionRowViewModel> OnRetry { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
