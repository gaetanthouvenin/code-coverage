// ------------------------------------------------------------------------------------------------
// <copyright file="MarkdownDocumentRendererTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Documentation.Rendering;

namespace CodeCoverage.Web.Tests.Documentation;

// The Markdown of a published document rendered on the server (M4.02o): raw HTML neutralised, anchors, links rewritten.
public sealed class MarkdownDocumentRendererTests
{
  #region Fields

  private const string Version = "1.2.3";

  private static readonly RenderLabels Labels = new("Link to this section", "Copy", "Copied.", "Refused.", "Note", "Tip", "Warning");

  private static readonly DocumentEntry Operations = DocumentationCatalog.Find("operations")!;

  private static readonly DocumentEntry Onboarding = DocumentationCatalog.Find("organization-onboarding")!;

  #endregion

  #region Methods

  [Fact]
  public void Render_FirstHeading_ShouldBecomeTheTitleAndTheNextParagraphTheLead()
  {
    var document = Render("# Operations\n\nHow to run it.\n\n## Alerts\n\nText.");

    document.Title.ShouldBe("Operations");
    document.Html.ShouldNotContain("<h1");
    document.Html.ShouldContain("<p class=\"cc-docs__lead\">How to run it.</p>");
  }

  // A threat case: the documents are trusted, but a raw HTML block or inline tag is still never rendered as HTML.
  [Theory]
  [InlineData("<script>alert(1)</script>")]
  [InlineData("Text <img src=x onerror=alert(1)> more")]
  [InlineData("<div onclick=\"alert(1)\">block</div>")]
  public void Render_RawHtml_ShouldBeEscaped(string markdown)
  {
    var html = Render($"# Title\n\n{markdown}").Html;

    html.ShouldNotContain("<script", Case.Insensitive);
    html.ShouldNotContain("<img", Case.Insensitive);
    html.ShouldNotContain("<div onclick", Case.Insensitive);
    html.ShouldContain("&lt;");
  }

  [Theory]
  [InlineData("javascript:alert(1)")]
  [InlineData("JAVASCRIPT:alert(1)")]
  [InlineData("data:text/html,hello")]
  [InlineData("vbscript:msgbox")]
  public void Render_LinkWithAnUnsafeScheme_ShouldKeepOnlyItsText(string url)
  {
    var html = Render($"# Title\n\nSee [the guide]({url}).").Html;

    html.ShouldNotContain("<a ");
    html.ShouldContain("See the guide.");
  }

  [Theory]
  [InlineData("[x](operations/organization-onboarding.md)", "docs/organization-onboarding")]
  [InlineData("[x](operations/organization-onboarding.md#2-admit-it)", "docs/organization-onboarding#2-admit-it")]
  [InlineData("[x](./deployment-azure.md)", "docs/deployment-azure")]
  [InlineData("[x](../action/README.md)", "docs/github-actions")]
  [InlineData("[x](#backups)", "docs/operations#backups")]
  public void Render_LinkToAPublishedDocument_ShouldLeadToItsPage(string markdown, string href)
  {
    var html = Render($"# Title\n\n{markdown}").Html;

    html.ShouldContain($"<a href=\"{href}\">x</a>");
  }

  [Theory]
  [InlineData("[x](ARCHITECTURE.md)", "https://github.com/gaetanthouvenin/code-coverage/blob/v1.2.3/docs/ARCHITECTURE.md")]
  [InlineData("[x](PLAN.md#m4-02o)", "https://github.com/gaetanthouvenin/code-coverage/blob/v1.2.3/docs/PLAN.md#m4-02o")]
  [InlineData("[x](../deploy/vm)", "https://github.com/gaetanthouvenin/code-coverage/blob/v1.2.3/deploy/vm")]
  [InlineData("[x](/scripts/Deploy-Production.ps1)", "https://github.com/gaetanthouvenin/code-coverage/blob/v1.2.3/scripts/Deploy-Production.ps1")]
  public void Render_LinkToAnUnpublishedFile_ShouldLeadToGitHubAtTheDeployedVersion(string markdown, string href)
  {
    var html = Render($"# Title\n\n{markdown}").Html;

    html.ShouldContain($"<a href=\"{href}\" class=\"cc-md__ext\">x</a>");
  }

  [Fact]
  public void Render_LinkLeavingTheRepository_ShouldKeepOnlyItsText()
  {
    var html = Render("# Title\n\n[x](../../../etc/passwd)").Html;

    html.ShouldNotContain("<a ");
  }

  [Fact]
  public void Render_ExternalLink_ShouldBeMarkedAsLeavingTheConsole()
  {
    var html = Render("# Title\n\n[GitHub](https://docs.github.com/en) and https://example.com/x").Html;

    html.ShouldContain("<a href=\"https://docs.github.com/en\" class=\"cc-md__ext\">GitHub</a>");
    html.ShouldContain("<a href=\"https://example.com/x\" class=\"cc-md__ext\">");
  }

  [Fact]
  public void Render_Headings_ShouldGetTheIdentifiersOfGitHubAndAnAnchorLink()
  {
    var document = Render("# Title\n\n## 2. Admit it\n\n### First deployment\n\n#### Deep", Onboarding);

    document.Headings.Select(static h => h.Id).ShouldBe(["2-admit-it", "first-deployment", "deep"]);
    document.Contents.ShouldHaveSingleItem().Text.ShouldBe("2. Admit it");
    document.Html.ShouldContain(
      "<h2 id=\"2-admit-it\">2. Admit it<a class=\"cc-md__anchor\" href=\"docs/organization-onboarding#2-admit-it\" aria-label=\"Link to this section\">#</a></h2>");
    document.Html.ShouldContain("<h4 id=\"deep\">Deep</h4>");
  }

  [Fact]
  public void Render_Table_ShouldUseTheClassesOfCcTable()
  {
    var html = Render("# Title\n\n| Setting | Default |\n|---|---|\n| `A` | b |").Html;

    html.ReplaceLineEndings(string.Empty).ShouldContain("<div class=\"cc-table-wrap\"><table class=\"cc-table\">");
    html.ShouldContain("<code>A</code>");
  }

  [Fact]
  public void Render_CodeBlock_ShouldBeCopyableWithItsLanguage()
  {
    var html = Render("# Title\n\n```bash\ndocker compose up -d <svc>\n```").Html;

    html.ShouldContain("<div class=\"cc-copy\"><pre class=\"cc-code\"><code>docker compose up -d &lt;svc&gt;\n</code></pre>");
    html.ShouldContain("<span class=\"cc-code__lang\" aria-hidden=\"true\">bash</span>");
    html.ShouldContain("data-cc-copy data-cc-copied=\"Copied.\" data-cc-copy-failed=\"Refused.\"");
    html.ShouldContain("role=\"status\" aria-live=\"polite\"");
  }

  [Theory]
  [InlineData("NOTE", "cc-callout--note", "Note")]
  [InlineData("TIP", "cc-callout--tip", "Tip")]
  [InlineData("WARNING", "cc-callout--warning", "Warning")]
  [InlineData("CAUTION", "cc-callout--warning", "Warning")]
  [InlineData("IMPORTANT", "cc-callout--note", "Note")]
  public void Render_GitHubAlert_ShouldBecomeACallout(string kind, string modifier, string title)
  {
    var html = Render($"# Title\n\n> [!{kind}]\n> Back up the certificate.").Html;

    html.ShouldContain($"<div class=\"cc-callout {modifier}\" role=\"note\">");
    html.ShouldContain($"<strong class=\"cc-callout__title\">{title}</strong>");
    html.ShouldContain("Back up the certificate.");
    html.ShouldNotContain("<blockquote");
  }

  private static RenderedDocument Render(string markdown, DocumentEntry? entry = null) =>
    MarkdownDocumentRenderer.Render(markdown, entry ?? Operations, "en", Version, Labels);

  #endregion
}
