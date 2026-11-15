// ------------------------------------------------------------------------------------------------
// <copyright file="WrappedTableRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>Renders a table with the classes of <c>CcTable</c>: a scrolling <c>cc-table-wrap</c> around a <c>cc-table</c>.</summary>
/// <param name="inner">The renderer of Markdig that writes the table itself.</param>
internal sealed class WrappedTableRenderer(IMarkdownObjectRenderer inner) : HtmlObjectRenderer<Table>
{
  #region Methods

  /// <inheritdoc />
  protected override void Write(HtmlRenderer renderer, Table obj)
  {
    ArgumentNullException.ThrowIfNull(renderer);
    ArgumentNullException.ThrowIfNull(obj);

    obj.GetAttributes().AddClass("cc-table");

    renderer.Write("<div class=\"cc-table-wrap\">");
    inner.Write(renderer, obj);
    renderer.WriteLine("</div>");
  }

  #endregion
}
