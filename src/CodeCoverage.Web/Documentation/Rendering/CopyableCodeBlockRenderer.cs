// ------------------------------------------------------------------------------------------------
// <copyright file="CopyableCodeBlockRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

using CodeCoverage.Web.Components.Ui.Icons;

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>Renders a code block as a <c>cc-copy</c> block: the code, its language and a « Copy » button.</summary>
/// <param name="labels">The localized texts of the rendering.</param>
/// <remarks>
///   The markup is not a component, so the button carries <c>data-cc-copy</c>: <c>cc-ui.js</c> copies the code of its block
///   and announces the result in the live region of the block.
/// </remarks>
internal sealed class CopyableCodeBlockRenderer(RenderLabels labels) : HtmlObjectRenderer<CodeBlock>
{
  #region Methods

  /// <inheritdoc />
  protected override void Write(HtmlRenderer renderer, CodeBlock obj)
  {
    ArgumentNullException.ThrowIfNull(renderer);
    ArgumentNullException.ThrowIfNull(obj);

    var language = (obj as FencedCodeBlock)?.Info;

    renderer.Write("<div class=\"cc-copy\"><pre class=\"cc-code\"><code>");
    renderer.WriteLeafRawLines(obj, true, true);
    renderer.Write("</code></pre><div class=\"cc-code__tools\">");

    if (!string.IsNullOrWhiteSpace(language))
    {
      renderer.Write("<span class=\"cc-code__lang\" aria-hidden=\"true\">").WriteEscape(language).Write("</span>");
    }

    renderer.Write("<button type=\"button\" class=\"cc-button cc-button--small\" data-cc-copy data-cc-copied=\"").WriteEscape(labels.Copied)
            .Write("\" data-cc-copy-failed=\"").WriteEscape(labels.CopyFailed).Write("\">")
            .Write(InlineIcon.Markup(CcIconName.Copy)).WriteEscape(labels.Copy)
            .Write("</button></div><span class=\"cc-sr-only\" role=\"status\" aria-live=\"polite\"></span></div>")
            .WriteLine();
  }

  #endregion
}
