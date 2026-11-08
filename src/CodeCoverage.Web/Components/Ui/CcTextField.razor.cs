// ------------------------------------------------------------------------------------------------
// <copyright file="CcTextField.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A labelled single-line pill input, with an optional help line and an optional error message,
///   both linked to the input through <c>aria-describedby</c>.
/// </summary>
/// <remarks>An error marks the input with <c>aria-invalid="true"</c> and a red border.</remarks>
public partial class CcTextField
{
  #region Fields

  private readonly string _generatedId = $"cc-text-{Guid.NewGuid():N}";

  #endregion

  #region Parameters

  /// <summary>Gets or sets the localized label, linked to the input.</summary>
  [Parameter]
  public string? Label { get; set; }

  /// <summary>Gets or sets the text (supports <c>@bind-Value</c>).</summary>
  [Parameter]
  public string? Value { get; set; }

  /// <summary>Gets or sets the callback raised on every keystroke with the new text.</summary>
  [Parameter]
  public EventCallback<string?> ValueChanged { get; set; }

  /// <summary>Gets or sets the placeholder.</summary>
  [Parameter]
  public string? Placeholder { get; set; }

  /// <summary>Gets or sets the native <c>type</c> of the input. Defaults to <c>text</c>.</summary>
  [Parameter]
  public string Type { get; set; } = "text";

  /// <summary>Gets or sets the native <c>autocomplete</c> hint, if any.</summary>
  [Parameter]
  public string? AutoComplete { get; set; }

  /// <summary>Gets or sets a value indicating whether the input is disabled.</summary>
  [Parameter]
  public bool Disabled { get; set; }

  /// <summary>Gets or sets a value indicating whether the input is read-only.</summary>
  [Parameter]
  public bool ReadOnly { get; set; }

  /// <summary>Gets or sets a value indicating whether a value is required.</summary>
  [Parameter]
  public bool Required { get; set; }

  /// <summary>Gets or sets the localized help line shown under the input.</summary>
  [Parameter]
  public string? Help { get; set; }

  /// <summary>Gets or sets the localized error message; when set, the input is marked invalid.</summary>
  [Parameter]
  public string? Error { get; set; }

  /// <summary>Gets or sets the identifier of the input; a unique one is generated when omitted.</summary>
  [Parameter]
  public string? Id { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the field.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the input.</summary>
  private string InputId => string.IsNullOrEmpty(Id) ? _generatedId : Id;

  /// <summary>Gets the identifier of the help line.</summary>
  private string HelpId => $"{InputId}-help";

  /// <summary>Gets the identifier of the error message.</summary>
  private string ErrorId => $"{InputId}-error";

  /// <summary>Gets a value indicating whether an error is shown.</summary>
  private bool HasError => !string.IsNullOrEmpty(Error);

  /// <summary>Gets the <c>aria-describedby</c> of the input, or <see langword="null" />.</summary>
  private string? DescribedBy
  {
    get
    {
      var ids = new List<string>(2);

      if (!string.IsNullOrEmpty(Help))
      {
        ids.Add(HelpId);
      }

      if (HasError)
      {
        ids.Add(ErrorId);
      }

      return ids.Count == 0 ? null : string.Join(' ', ids);
    }
  }

  /// <summary>Gets the CSS classes of the input.</summary>
  private string InputClass => HasError ? "cc-input cc-input--invalid" : "cc-input";

  /// <summary>Gets the CSS classes of the field.</summary>
  private string CssClass => "cc-field" + CcCss.Extra(Class);

  #endregion

  #region Methods

  private Task OnInputAsync(ChangeEventArgs args) =>
    ValueChanged.InvokeAsync(args.Value?.ToString());

  #endregion
}
