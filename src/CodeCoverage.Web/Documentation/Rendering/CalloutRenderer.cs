// ------------------------------------------------------------------------------------------------
// <copyright file="CalloutRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Markdig.Extensions.Alerts;
using Markdig.Renderers;
using Markdig.Renderers.Html;

using CodeCoverage.Web.Components.Ui.Icons;

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>
///   Renders a GitHub alert (<c>&gt; [!NOTE]</c>, <c>[!TIP]</c>, <c>[!WARNING]</c>…) as a callout <c>cc-callout--note</c>,
///   <c>--tip</c> or <c>--warning</c>.
/// </summary>
/// <param name="labels">The localized texts of the rendering.</param>
internal sealed class CalloutRenderer(RenderLabels labels) : HtmlObjectRenderer<AlertBlock>
{
  #region Methods

  /// <inheritdoc />
  protected override void Write(HtmlRenderer renderer, AlertBlock obj)
  {
    ArgumentNullException.ThrowIfNull(renderer);
    ArgumentNullException.ThrowIfNull(obj);

    var (modifier, icon, title) = obj.Kind.ToString().ToUpperInvariant() switch
    {
      "TIP" => ("tip", CcIconName.Check, labels.Tip),
      "WARNING" or "CAUTION" => ("warning", CcIconName.Alert, labels.Warning),
      var _ => ("note", CcIconName.Info, labels.Note),
    };

    renderer.Write("<div class=\"cc-callout cc-callout--").Write(modifier).Write("\" role=\"note\">").Write(InlineIcon.Markup(icon))
            .Write("<div><strong class=\"cc-callout__title\">").WriteEscape(title).Write("</strong>");
    renderer.WriteChildren(obj);
    renderer.WriteLine("</div></div>");
  }

  #endregion
}
