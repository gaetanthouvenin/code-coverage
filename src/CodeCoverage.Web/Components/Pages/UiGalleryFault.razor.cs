// ------------------------------------------------------------------------------------------------
// <copyright file="UiGalleryFault.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>A sample of the <c>/_ui</c> gallery that throws on demand, to show the error boundary.</summary>
public partial class UiGalleryFault
{
  #region Parameters

  /// <summary>Gets or sets a value indicating whether the sample throws when rendered.</summary>
  [Parameter]
  public bool Fail { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  /// <exception cref="InvalidOperationException"><see cref="Fail" /> is <see langword="true" />.</exception>
  protected override void OnParametersSet()
  {
    if (Fail)
    {
      throw new InvalidOperationException("Sample failure of the UI gallery.");
    }
  }

  #endregion
}
