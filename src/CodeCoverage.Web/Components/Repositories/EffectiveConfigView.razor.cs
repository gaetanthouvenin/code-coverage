// ------------------------------------------------------------------------------------------------
// <copyright file="EffectiveConfigView.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The configuration in force for a repository, read-only: its source, the YAML as it is written, and what was
///   refused or not applied. Nothing is validated here; the server already said what it read.
/// </summary>
public partial class EffectiveConfigView
{
  #region Parameters

  /// <summary>Gets or sets the configuration to show.</summary>
  [Parameter, EditorRequired]
  public EffectiveConfigViewModel Config { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
