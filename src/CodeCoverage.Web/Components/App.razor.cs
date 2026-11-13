// ------------------------------------------------------------------------------------------------
// <copyright file="App.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Components.Web;

namespace CodeCoverage.Web.Components;

/// <summary>The document of the console: the head, the styles and the router.</summary>
/// <remarks>
///   Every page runs in a circuit, except the ones marked
///   <see cref="ExcludeFromInteractiveRoutingAttribute" />: the first-start wizard renders on the server alone, since
///   it reads and writes the cookie that carries its token, which a circuit cannot reach.
/// </remarks>
public partial class App
{
  #region Parameters

  /// <summary>Gets or sets the context of the request that renders the document.</summary>
  [CascadingParameter]
  private HttpContext HttpContext { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the render mode of the page asked, or <see langword="null" /> for a page rendered on the server alone.</summary>
  private IComponentRenderMode? PageRenderMode =>
    HttpContext.GetEndpoint()?.Metadata.GetMetadata<ExcludeFromInteractiveRoutingAttribute>() is null ? RenderMode.InteractiveServer : null;

  #endregion
}
