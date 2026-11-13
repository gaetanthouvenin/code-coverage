// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLogView.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>
///   The processing log of an upload: its translated error code, its unresolved or ambiguous paths (the first ones shown,
///   the others behind a disclosure) and its messages, those with a known code translated and the others as stored.
/// </summary>
public partial class ProcessingLogView
{
  #region Fields

  // The paths shown before the disclosure: a log keeps up to 200 of them.
  private const int VisiblePaths = 3;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the row whose log is shown.</summary>
  [Parameter, EditorRequired]
  public UploadSessionRowViewModel Row { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private ErrorCodeLocalizer Errors { get; set; } = null!;

  #endregion

  #region Methods

  // A message whose code has a translation reads « {translation} ({argument}) »; any other one is shown as stored.
  private string Text(LogMessageViewModel message)
  {
    if (message.Code is { } code && L[code] is { ResourceNotFound: false } translated)
    {
      return string.IsNullOrEmpty(message.Text) ? translated.Value : $"{translated.Value} ({message.Text})";
    }

    return message.Code is null ? message.Text : $"{message.Code}: {message.Text}";
  }

  #endregion
}
