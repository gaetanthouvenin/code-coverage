// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationSourceTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Documentation;

// The whitelist of the in-app documentation and the links that lead into it (M4.02o): every published document is
// embedded and rendered, nothing else of docs/ is published, and every help link of the console targets a real anchor.
public sealed partial class DocumentationSourceTests
{
  #region Fields

  private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(WebRootFiles.ProjectDirectory, "..", ".."));

  private readonly DocumentationLibrary _library;

  #endregion

  #region Ctors

  public DocumentationSourceTests()
  {
    var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();

    _library = new DocumentationLibrary(services.GetRequiredService<IStringLocalizer<Common>>());
  }

  #endregion

  #region Properties

  public static TheoryData<string> Slugs => [.. DocumentationCatalog.Entries.Select(static e => e.Slug)];

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public void Catalogue_ShouldMatchTheDocumentsEmbeddedByTheProject()
  {
    var project = XDocument.Load(Path.Combine(WebRootFiles.ProjectDirectory, "CodeCoverage.Web.csproj"));
    var embedded = project.Descendants("PublishedDocument")
                          .Select(e => (Slug: (string)e.Attribute("Slug")!, Source: RepositoryPath((string)e.Attribute("Include")!)))
                          .ToList();

    embedded.ShouldBe(DocumentationCatalog.Entries.Select(static e => (e.Slug, Source: e.SourcePath)));
    DocumentationCatalog.Entries.ShouldAllBe(e => File.Exists(Path.Combine(RepositoryRoot, e.SourcePath)));
  }

  // The image of the Web builds from a context that leaves docs/ out: each published document is let back in.
  [Fact]
  public void DockerIgnore_ShouldLetEveryPublishedDocumentIntoTheImageBuild()
  {
    var lines = File.ReadAllLines(Path.Combine(RepositoryRoot, ".dockerignore")).Select(static l => l.Trim()).ToHashSet(StringComparer.Ordinal);

    DocumentationCatalog.Entries.Select(static e => e.SourcePath)
                        .Where(p => !lines.Contains($"!{p}"))
                        .ShouldBeEmpty("Dockerfile.web embeds the published documents: .dockerignore must not filter them out");
  }

  // The plan, the ADRs, ARCHITECTURE and CONVENTIONS are never published.
  [Fact]
  public void Catalogue_ShouldNeverPublishTheInternalDocuments()
  {
    DocumentationCatalog.Entries.Select(static e => e.SourcePath)
                        .ShouldAllBe(p => p != "docs/PLAN.md"
                                          && p != "docs/ARCHITECTURE.md"
                                          && p != "docs/CONVENTIONS.md"
                                          && !p.StartsWith("docs/adr/", StringComparison.Ordinal)
                                          && !p.StartsWith("docs/design/", StringComparison.Ordinal));
  }

  [Theory]
  [MemberData(nameof(Slugs))]
  public async Task GetAsync_PublishedDocument_ShouldBeEmbeddedAndRendered(string slug)
  {
    var document = await _library.GetAsync(DocumentationCatalog.Find(slug)!, Ct);

    document.Title.ShouldNotBe(slug);
    document.Html.ShouldNotBeNullOrWhiteSpace();
    document.Language.ShouldBe("en");
  }

  // The source check of the plan: each help link of the console (a "docs/{slug}#{anchor}" literal of the Web, the
  // constants of DocumentationLinks included) names a published page and an anchor of it that exists.
  [Fact]
  public async Task HelpLinks_OfTheConsole_ShouldEachTargetAnExistingAnchor()
  {
    var links = Directory.EnumerateFiles(WebRootFiles.ProjectDirectory, "*.*", SearchOption.AllDirectories)
                         .Where(static f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal))
                         .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                            && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                         .SelectMany(static f => HelpLink().Matches(File.ReadAllText(f)).Select(static m => m.Groups["href"].Value))
                         .Distinct(StringComparer.Ordinal)
                         .ToList();

    links.ShouldContain(DocumentationLinks.Setup);
    (await BrokenAsync(links)).ShouldBeEmpty("every help link names a published page and one of its headings");
  }

  // The links between published documents are rewritten to their pages: their anchors must exist too.
  [Fact]
  public async Task LinksBetweenDocuments_ShouldEachTargetAnExistingAnchor()
  {
    var links = new List<string>();

    foreach (var entry in DocumentationCatalog.Entries)
    {
      var html = (await _library.GetAsync(entry, Ct)).Html;

      links.AddRange(RenderedLink().Matches(html).Select(static m => m.Groups["href"].Value));
    }

    (await BrokenAsync(links)).ShouldBeEmpty("a link between documents names a heading that exists");
  }

  // The « ? » of the screens of the mockup; /admin/settings has none until a document describes the instance settings.
  [Theory]
  [InlineData("Components/Pages/Setup.razor", nameof(DocumentationLinks.Setup))]
  [InlineData("Components/Pages/AdminOrganizations.razor", nameof(DocumentationLinks.AdmitAccount))]
  [InlineData("Components/Pages/OrganizationSettings.razor", nameof(DocumentationLinks.OrganizationSettings))]
  [InlineData("Components/Pages/OrganizationUploadTokens.razor", nameof(DocumentationLinks.UploadTokens))]
  [InlineData("Components/Pages/RepositoryOverview.razor", nameof(DocumentationLinks.FirstUpload))]
  public void Screen_OfTheMockup_ShouldCarryItsContextualHelp(string page, string link) =>
    WebRootFiles.Read(page).ShouldContain($"DocumentationLinks.{link}");

  // MSBuild writes the Include with backslashes, which are no separator on Linux: they are made slashes first.
  private static string RepositoryPath(string include) =>
    Path.GetRelativePath(RepositoryRoot, Path.GetFullPath(Path.Combine(WebRootFiles.ProjectDirectory, include.Replace('\\', '/')))).Replace('\\', '/');

  private async Task<List<string>> BrokenAsync(IEnumerable<string> links)
  {
    var broken = new List<string>();

    foreach (var link in links)
    {
      var target = link[DocumentationCatalog.PathPrefix.Length..];
      var hash = target.IndexOf('#', StringComparison.Ordinal);
      var entry = DocumentationCatalog.Find(hash < 0 ? target : target[..hash]);

      if (entry is null || (hash >= 0 && !(await _library.GetAsync(entry, Ct)).HasAnchor(target[(hash + 1)..])))
      {
        broken.Add(link);
      }
    }

    return broken;
  }

  [GeneratedRegex("\"(?<href>docs/[a-z0-9-]+(?:#[^\"]+)?)\"")]
  private static partial Regex HelpLink();

  [GeneratedRegex("href=\"(?<href>docs/[a-z0-9-]+(?:#[^\"]+)?)\"")]
  private static partial Regex RenderedLink();

  #endregion
}
