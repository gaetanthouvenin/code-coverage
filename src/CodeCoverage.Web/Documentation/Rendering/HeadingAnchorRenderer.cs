// ------------------------------------------------------------------------------------------------
// <copyright file="HeadingAnchorRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>Renders a heading with its identifier and, for the levels 2 and 3, the « # » link to it (<c>cc-md__anchor</c>).</summary>
/// <param name="entry">The document rendered: the link names its page, since the base address of the console is <c>/</c>.</param>
/// <param name="labels">The localized texts of the rendering.</param>
internal sealed class HeadingAnchorRenderer(DocumentEntry entry, RenderLabels labels) : HtmlObjectRenderer<HeadingBlock>
{
  #region Methods

  /// <inheritdoc />
  protected override void Write(HtmlRenderer renderer, HeadingBlock obj)
  {
    ArgumentNullException.ThrowIfNull(renderer);
    ArgumentNullException.ThrowIfNull(obj);

    var tag = "h" + Math.Clamp(obj.Level, 1, 6).ToString(CultureInfo.InvariantCulture);

    renderer.Write('<').Write(tag).WriteAttributes(obj).Write('>');
    renderer.WriteLeafInline(obj);

    if (obj.Level is 2 or 3 && obj.GetAttributes().Id is { Length: > 0 } id)
    {
      renderer.Write("<a class=\"cc-md__anchor\" href=\"").WriteEscape(entry.Href).Write('#').WriteEscape(id)
              .Write("\" aria-label=\"").WriteEscape(labels.Anchor).Write("\">#</a>");
    }

    renderer.Write("</").Write(tag).WriteLine(">");
  }

  #endregion
}
