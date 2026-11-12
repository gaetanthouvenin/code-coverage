// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigWarnings.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>The configuration applied to a commit: its source and path, and the warnings raised while it was read (never its raw YAML).</summary>
public partial class ConfigWarnings
{
  #region Parameters

  /// <summary>Gets or sets the configuration.</summary>
  [Parameter, EditorRequired]
  public CommitConfigViewModel Config { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
