// ------------------------------------------------------------------------------------------------
// <copyright file="MarkdownDocumentRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>Renders the Markdown of a published document to the HTML of the console (<c>cc-md</c>).</summary>
/// <remarks>
///   Raw HTML is never rendered: Markdig parses it as text, which the renderer escapes. The headings get the identifiers
///   of GitHub, so an anchor of the repository works in the console; the first heading becomes the title of the page and
///   the paragraph after it its lead.
/// </remarks>
internal static class MarkdownDocumentRenderer
{
  #region Fields

  /// <summary>The pipeline: GitHub tables, alerts, identifiers, bare links and the YAML header of a translation, without raw HTML.</summary>
  private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
                                                      .UsePipeTables()
                                                      .UseAlertBlocks()
                                                      .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
                                                      .UseAutoLinks()
                                                      .UseYamlFrontMatter()
                                                      .DisableHtml()
                                                      .Build();

  #endregion

  #region Methods

  /// <summary>Renders a document.</summary>
  /// <param name="markdown">The Markdown of the document.</param>
  /// <param name="entry">The published document.</param>
  /// <param name="language">The language of the Markdown, <c>en</c> or the language of a translation.</param>
  /// <param name="version">The deployed version, without the leading <c>v</c>.</param>
  /// <param name="labels">The localized texts of the rendering.</param>
  /// <param name="anchors">
  ///   The headings of the English document when <paramref name="markdown" /> is a translation: its headings take their
  ///   identifiers, in order, while their levels match; <see langword="null" /> keeps the identifiers of the text.
  /// </param>
  /// <returns>The rendered document.</returns>
  /// <exception cref="ArgumentNullException">A required argument is <see langword="null" />.</exception>
  public static RenderedDocument Render(
    string markdown,
    DocumentEntry entry,
    string language,
    string version,
    RenderLabels labels,
    IReadOnlyList<DocumentHeading>? anchors = null)
  {
    ArgumentNullException.ThrowIfNull(markdown);
    ArgumentNullException.ThrowIfNull(entry);
    ArgumentNullException.ThrowIfNull(language);
    ArgumentNullException.ThrowIfNull(version);
    ArgumentNullException.ThrowIfNull(labels);

    var document = Markdown.Parse(markdown, Pipeline);

    // The YAML header of a translation (source-sha256) is checked by the tests, never shown.
    foreach (var header in document.OfType<YamlFrontMatterBlock>().ToList())
    {
      document.Remove(header);
    }

    var title = TakeTitle(document) ?? entry.Slug;

    if (anchors is not null)
    {
      TakeAnchors(document, anchors);
    }

    DocumentLinkRewriter.Rewrite(document, entry, version);

    var headings = document.Descendants<HeadingBlock>()
                           .Where(static h => h.Level >= 2 && h.GetAttributes().Id is { Length: > 0 })
                           .Select(static h => new DocumentHeading(h.GetAttributes().Id!, PlainText(h.Inline), h.Level))
                           .ToList();

    using var writer = new StringWriter();
    var renderer = new HtmlRenderer(writer);

    Pipeline.Setup(renderer);
    Customize(renderer, entry, labels);
    renderer.Render(document);
    writer.Flush();

    return new RenderedDocument(entry, title, writer.ToString(), headings, language, language);
  }

  /// <summary>Gives the headings of a translation the identifiers of the English headings, in order.</summary>
  /// <param name="document">The parsed translation, without its title.</param>
  /// <param name="anchors">The headings of the English document.</param>
  /// <remarks>A translation has the plan of the English document (a test checks it); the copy stops at the first difference of level.</remarks>
  private static void TakeAnchors(MarkdownDocument document, IReadOnlyList<DocumentHeading> anchors)
  {
    var headings = document.Descendants<HeadingBlock>().Where(static h => h.Level >= 2).ToList();

    for (var i = 0; i < headings.Count && i < anchors.Count && headings[i].Level == anchors[i].Level; i++)
    {
      headings[i].GetAttributes().Id = anchors[i].Id;
    }
  }

  /// <summary>Replaces the renderers of Markdig that the console draws its own way.</summary>
  /// <param name="renderer">The HTML renderer, set up by the pipeline.</param>
  /// <param name="entry">The document rendered.</param>
  /// <param name="labels">The localized texts of the rendering.</param>
  private static void Customize(HtmlRenderer renderer, DocumentEntry entry, RenderLabels labels)
  {
    var renderers = renderer.ObjectRenderers;

    renderers.ReplaceOrAdd<HeadingRenderer>(new HeadingAnchorRenderer(entry, labels));
    renderers.ReplaceOrAdd<CodeBlockRenderer>(new CopyableCodeBlockRenderer(labels));
    renderers.ReplaceOrAdd<AlertBlockRenderer>(new CalloutRenderer(labels));

    if (renderers.FindExact<HtmlTableRenderer>() is { } table)
    {
      renderers.ReplaceOrAdd<HtmlTableRenderer>(new WrappedTableRenderer(table));
    }
  }

  /// <summary>Removes the first-level heading that opens the document, and marks the paragraph after it as the lead.</summary>
  /// <param name="document">The parsed document.</param>
  /// <returns>The text of the heading, or <see langword="null" /> when the document does not open with one.</returns>
  private static string? TakeTitle(MarkdownDocument document)
  {
    if (document.Count == 0 || document[0] is not HeadingBlock { Level: 1 } heading)
    {
      return null;
    }

    document.RemoveAt(0);

    if (document.Count > 0 && document[0] is ParagraphBlock lead)
    {
      lead.GetAttributes().AddClass("cc-docs__lead");
    }

    return PlainText(heading.Inline);
  }

  /// <summary>Reads the text of inlines, without their formatting.</summary>
  /// <param name="inline">The inlines of a heading.</param>
  /// <returns>The text.</returns>
  private static string PlainText(ContainerInline? inline)
  {
    if (inline is null)
    {
      return string.Empty;
    }

    var text = new StringBuilder();

    foreach (var child in inline.Descendants())
    {
      switch (child)
      {
        case LiteralInline literal:
          text.Append(literal.Content.AsSpan());
          break;
        case CodeInline code:
          text.Append(code.Content);
          break;
      }
    }

    return text.ToString().Trim();
  }

  #endregion
}
