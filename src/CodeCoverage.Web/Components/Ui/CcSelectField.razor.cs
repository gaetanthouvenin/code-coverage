// ------------------------------------------------------------------------------------------------
// <copyright file="CcSelectField.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A labelled native select with a round chevron; with <see cref="Pill" />, the large pill
///   select of the page headers and toolbars, whose label is read by screen readers only.
/// </summary>
public partial class CcSelectField
{
  #region Fields

  private readonly string _generatedId = $"cc-select-{Guid.NewGuid():N}";

  #endregion

  #region Parameters

  /// <summary>Gets or sets the localized label, linked to the select.</summary>
  [Parameter]
  public string? Label { get; set; }

  /// <summary>Gets or sets the selected value (supports <c>@bind-Value</c>).</summary>
  [Parameter]
  public string? Value { get; set; }

  /// <summary>Gets or sets the callback raised when another option is selected.</summary>
  [Parameter]
  public EventCallback<string?> ValueChanged { get; set; }

  /// <summary>Gets or sets the options.</summary>
  [Parameter]
  public IReadOnlyList<CcSelectOption> Options { get; set; } = [];

  /// <summary>Gets or sets the text of an empty first option, if any.</summary>
  [Parameter]
  public string? Placeholder { get; set; }

  /// <summary>Gets or sets a value indicating whether the select is disabled.</summary>
  [Parameter]
  public bool Disabled { get; set; }

  /// <summary>Gets or sets the localized help line shown under the select.</summary>
  [Parameter]
  public string? Help { get; set; }

  /// <summary>Gets or sets a value indicating whether the select is drawn as a large pill.</summary>
  [Parameter]
  public bool Pill { get; set; }

  /// <summary>Gets or sets the icon shown inside the select, before the value, if any.</summary>
  [Parameter]
  public CcIconName? LeadIcon { get; set; }

  /// <summary>Gets or sets the identifier of the select; a unique one is generated when omitted.</summary>
  [Parameter]
  public string? Id { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the field.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the select.</summary>
  private string SelectId => string.IsNullOrEmpty(Id) ? _generatedId : Id;

  /// <summary>Gets the identifier of the help line.</summary>
  private string HelpId => $"{SelectId}-help";

  /// <summary>Gets the CSS classes of the wrapper of the select.</summary>
  private string WrapperClass =>
    "cc-select" + (Pill ? " cc-select--pill" : string.Empty) + (LeadIcon is null ? string.Empty : " cc-select--lead");

  /// <summary>Gets the CSS classes of the field.</summary>
  private string CssClass => "cc-field" + CcCss.Extra(Class);

  #endregion

  #region Methods

  private Task OnChangeAsync(ChangeEventArgs args) =>
    ValueChanged.InvokeAsync(args.Value?.ToString());

  #endregion
}
