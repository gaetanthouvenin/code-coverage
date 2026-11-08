// ------------------------------------------------------------------------------------------------
// <copyright file="ThrowingComponent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CodeCoverage.Web.Tests.Support;

// Renders "healthy", or throws while the shared switch says so (read at every render).
public sealed class ThrowingComponent : ComponentBase
{
  #region Properties

  [Parameter]
  public Func<bool> ShouldFail { get; set; } = () => false;

  #endregion

  #region Methods

  protected override void BuildRenderTree(RenderTreeBuilder builder)
  {
    if (ShouldFail())
    {
      throw new InvalidOperationException("Rendering failed.");
    }

    builder.AddMarkupContent(0, "<p class=\"healthy\">healthy</p>");
  }

  #endregion
}
