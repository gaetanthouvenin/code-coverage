// ------------------------------------------------------------------------------------------------
// <copyright file="CcSwitch.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>An on/off switch, for a value that applies as soon as it changes.</summary>
public partial class CcSwitch
{
  #region Parameters

  /// <summary>Gets or sets a value indicating whether the switch is on.</summary>
  [Parameter]
  public bool Value { get; set; }

  /// <summary>Gets or sets what happens when the user turns the switch, with its new value.</summary>
  [Parameter]
  public EventCallback<bool> ValueChanged { get; set; }

  /// <summary>Gets or sets the accessible name of the switch.</summary>
  [Parameter, EditorRequired]
  public string Label { get; set; } = null!;

  /// <summary>Gets or sets a value indicating whether the switch is read-only.</summary>
  [Parameter]
  public bool Disabled { get; set; }

  /// <summary>Gets or sets extra CSS classes.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  private string CssClass => "cc-switch" + (Value ? " cc-switch--on" : string.Empty) + CcCss.Extra(Class);

  #endregion

  #region Methods

  private Task ToggleAsync() => ValueChanged.InvokeAsync(!Value);

  #endregion
}
