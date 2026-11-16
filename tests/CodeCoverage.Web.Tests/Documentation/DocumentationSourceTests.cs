// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationSourceTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Markdig;
using Markdig.Syntax;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Documentation;

// The whitelist of the in-app documentation and the links that lead into it (M4.02o): every published document is
// embedded and rendered, nothing else of docs/ is published, and every help link of the console targets a real anchor.
// Each translation (M4.02v) is embedded, keeps the plan of its English document and names the hash of the text it
// translates; the links hold in every language.
public sealed partial class DocumentationSourceTests : IDisposable
{
  #region Fields

  private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(WebRootFiles.ProjectDirectory, "..", ".."));

  private static readonly MarkdownPipeline HeadingPipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();

  private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

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

  public static TheoryData<string> Languages => [.. ConsoleLanguages.Codes];

  // The languages of the translations, from the property DocumentationLanguages of the Web project.
  private static IReadOnlyList<string> DocumentationLanguages =>
    [.. XDocument.Load(Path.Combine(WebRootFiles.ProjectDirectory, "CodeCoverage.Web.csproj"))
                 .Descendants("DocumentationLanguages")
                 .Single()
                 .Value
                 .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

  // Each translation written next to a published document: the document, the language and the path from the root.
  private static IReadOnlyList<(DocumentEntry Entry, string Language, string Path)> Translations =>
    [.. DocumentationCatalog.Entries.SelectMany(static e => DocumentationLanguages.Select(l => (Entry: e, Language: l, Path: TranslationPath(e, l))))
                            .Where(static t => File.Exists(System.IO.Path.Combine(RepositoryRoot, t.Path)))];

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

    DocumentationCatalog.Entries.SelectMany(static e => DocumentationLanguages.Select(l => TranslationPath(e, l)).Prepend(e.SourcePath))
                        .Where(p => !lines.Contains($"!{p}"))
                        .ShouldBeEmpty("Dockerfile.web embeds the published documents and their translations: .dockerignore must not filter them out");
  }

  // The translations are of the languages of the console, English aside.
  [Fact]
  public void DocumentationLanguages_ShouldBeTheLanguagesOfTheConsoleButEnglish()
  {
    DocumentationLanguages.ShouldBe(ConsoleLanguages.Codes.Where(static c => c != ConsoleLanguages.DefaultCode));
  }

  // Each translation is embedded under the name the library reads, and no embedded translation lacks its document.
  [Fact]
  public void Translations_ShouldEachBeEmbeddedNextToTheirEnglishDocument()
  {
    var assembly = typeof(DocumentationLibrary).Assembly;
    var expected = Translations.Select(static t => DocumentationLibrary.ResourceName(t.Entry.Slug, t.Language)).Order(StringComparer.Ordinal).ToList();
    var embedded = assembly.GetManifestResourceNames()
                           .Where(static n => n.StartsWith(DocumentationCatalog.ResourcePrefix, StringComparison.Ordinal) && n.Count(static c => c == '.') > DocumentationCatalog.ResourcePrefix.Count(static c => c == '.') + 1)
                           .Order(StringComparer.Ordinal)
                           .ToList();

    embedded.ShouldBe(expected);
  }

  // The anchors of a translation are those of the English headings, in order: both have the same plan of headings.
  [Fact]
  public void Translations_ShouldKeepThePlanOfHeadingsOfTheirEnglishDocument()
  {
    var different = Translations.Where(static t => !HeadingLevels(t.Entry.SourcePath).SequenceEqual(HeadingLevels(t.Path)))
                                .Select(static t => t.Path)
                                .ToList();

    different.ShouldBeEmpty("a translation has the headings of its English document: same levels, same number, same order");
  }

  // A change to an English document without its translations fails here: each names the hash of the text it translates.
  [Fact]
  public void Translations_ShouldNameTheHashOfTheirEnglishDocument()
  {
    var stale = Translations.Select(static t => (t.Path, Named: NamedHash(t.Path), Expected: Hash(t.Entry.SourcePath)))
                            .Where(static t => t.Named != t.Expected)
                            .Select(static t => $"{t.Path}: source-sha256: {t.Expected} (named: {t.Named ?? "none"})")
                            .ToList();

    stale.ShouldBeEmpty("update each translation to its English document, then write the expected source-sha256");
  }

  // The help links of the console open an existing section in every language.
  [Theory]
  [MemberData(nameof(Languages))]
  public async Task HelpLinks_InEachLanguage_ShouldEachTargetAnExistingAnchor(string language)
  {
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);

    (await BrokenAsync([.. HelpLinks()])).ShouldBeEmpty($"every help link names a heading of its page in {language}");
  }

  // The links between published documents keep their targets in every language.
  [Theory]
  [MemberData(nameof(Languages))]
  public async Task LinksBetweenDocuments_InEachLanguage_ShouldEachTargetAnExistingAnchor(string language)
  {
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);

    (await BrokenAsync(await RenderedLinksAsync())).ShouldBeEmpty($"a link between documents names a heading that exists in {language}");
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
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

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
    var links = HelpLinks();

    links.ShouldContain(DocumentationLinks.Setup);
    (await BrokenAsync(links)).ShouldBeEmpty("every help link names a published page and one of its headings");
  }

  // The links between published documents are rewritten to their pages: their anchors must exist too.
  [Fact]
  public async Task LinksBetweenDocuments_ShouldEachTargetAnExistingAnchor()
  {
    var links = await RenderedLinksAsync();

    (await BrokenAsync(links)).ShouldBeEmpty("a link between documents names a heading that exists");
  }

  // The « ? » of the screens of the mockup; /admin/settings has none until a document describes the instance settings.
  [Theory]
  [InlineData("Components/Pages/Setup.razor", nameof(DocumentationLinks.Setup))]
  [InlineData("Components/Pages/AdminOrganizations.razor", nameof(DocumentationLinks.AdmitAccount))]
  [InlineData("Components/Pages/OrganizationSettings.razor", nameof(DocumentationLinks.OrganizationSettings))]
  [InlineData("Components/Pages/OrganizationUploadTokens.razor", nameof(DocumentationLinks.UploadTokens))]
  [InlineData("Components/Pages/RepositoryOverview.razor", nameof(DocumentationLinks.FirstUpload))]
  [InlineData("Components/Pages/CommitReport.razor", nameof(DocumentationLinks.DetailedReport))]
  public void Screen_OfTheMockup_ShouldCarryItsContextualHelp(string page, string link) =>
    WebRootFiles.Read(page).ShouldContain($"DocumentationLinks.{link}");

  // The help links of the console: each "docs/{slug}#{anchor}" literal of the Web, the constants of DocumentationLinks included.
  private static List<string> HelpLinks() =>
    Directory.EnumerateFiles(WebRootFiles.ProjectDirectory, "*.*", SearchOption.AllDirectories)
             .Where(static f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal))
             .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
             .SelectMany(static f => HelpLink().Matches(File.ReadAllText(f)).Select(static m => m.Groups["href"].Value))
             .Distinct(StringComparer.Ordinal)
             .ToList();

  // The path from the root of the translation of a document: name.{language}.md next to it.
  private static string TranslationPath(DocumentEntry entry, string language) =>
    $"{entry.SourcePath[..^".md".Length]}.{language}.md";

  // The text of a document of the repository, with LF line endings: the hash does not depend on the working copy.
  private static string ReadNormalized(string path) =>
    File.ReadAllText(Path.Combine(RepositoryRoot, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

  // The levels of the headings of a document, in order, its YAML header and code blocks aside.
  private static List<int> HeadingLevels(string path) =>
    [.. Markdown.Parse(ReadNormalized(path), HeadingPipeline).Descendants<HeadingBlock>().Select(static h => h.Level)];

  private static string Hash(string path) =>
    Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ReadNormalized(path))));

  // The source-sha256 of the YAML header that opens a translation, or null.
  private static string? NamedHash(string path)
  {
    var match = SourceHash().Match(ReadNormalized(path));

    return match.Success ? match.Groups["hash"].Value : null;
  }

  // MSBuild writes the Include with backslashes, which are no separator on Linux: they are made slashes first.
  private static string RepositoryPath(string include) =>
    Path.GetRelativePath(RepositoryRoot, Path.GetFullPath(Path.Combine(WebRootFiles.ProjectDirectory, include.Replace('\\', '/')))).Replace('\\', '/');

  private async Task<List<string>> RenderedLinksAsync()
  {
    var links = new List<string>();

    foreach (var entry in DocumentationCatalog.Entries)
    {
      var html = (await _library.GetAsync(entry, Ct)).Html;

      links.AddRange(RenderedLink().Matches(html).Select(static m => m.Groups["href"].Value));
    }

    return links;
  }

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

  [GeneratedRegex(@"\A---\n(?:.*\n)*?source-sha256:\s*(?<hash>[0-9a-f]{64})\s*\n(?:.*\n)*?---\n")]
  private static partial Regex SourceHash();

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => CultureInfo.CurrentUICulture = _uiCulture;

  #endregion
}
