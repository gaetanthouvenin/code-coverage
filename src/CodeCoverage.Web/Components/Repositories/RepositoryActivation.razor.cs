// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryActivation.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The state of a repository, with the switch that enables or disables it. The switch is bound to the state the
///   server last gave: a refusal leaves it where the server says, never where it was clicked.
/// </summary>
public partial class RepositoryActivation
{
  #region Parameters

  /// <summary>Gets or sets a value indicating whether the repository accepts uploads and publishes.</summary>
  [Parameter]
  public bool IsActive { get; set; }

  /// <summary>Gets or sets a value indicating whether a change is being sent: the switch waits for the answer.</summary>
  [Parameter]
  public bool Busy { get; set; }

  /// <summary>Gets or sets what happens when the user asks for the other state.</summary>
  [Parameter]
  public EventCallback<bool> OnChanged { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private string ThumbClass => IsActive ? "cc-thumb cc-thumb--ok" : "cc-thumb";

  #endregion

  #region Methods

  private Task ToggleAsync(bool active) => OnChanged.InvokeAsync(active);

  #endregion
}
