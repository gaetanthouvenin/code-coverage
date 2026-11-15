// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationLibrary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Documentation.Rendering;
using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Documentation;

/// <summary>Reads and renders the published documents embedded in the Web, once per document and language of the console.</summary>
/// <remarks>
///   The documents are written in English. A translation <c>{name}.{language}.md</c> next to a document is served when
///   the console speaks that language (M4.02v); without one, the English document is served and marked as a fallback. A
///   translation takes the anchors of the English headings, in order, so that a link or a contextual help opens the same
///   section in every language. The rendered documents are kept for the life of the host: the embedded documents never
///   change.
/// </remarks>
public sealed class DocumentationLibrary
{
  #region Fields

  /// <summary>The texts of the console, read in the culture of the request.</summary>
  private readonly IStringLocalizer<Common> _localizer;

  /// <summary>The assembly that embeds the documents.</summary>
  private readonly Assembly _documents;

  /// <summary>The rendered documents, by slug and language of the console.</summary>
  private readonly ConcurrentDictionary<string, Lazy<Task<RenderedDocument>>> _rendered = new(StringComparer.Ordinal);

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="DocumentationLibrary" /> class on the documents of the Web.</summary>
  /// <param name="localizer">The texts of the console, read in the culture of the request.</param>
  public DocumentationLibrary(IStringLocalizer<Common> localizer)
    : this(localizer, typeof(DocumentationLibrary).Assembly)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="DocumentationLibrary" /> class on the documents of an assembly.</summary>
  /// <param name="localizer">The texts of the console, read in the culture of the request.</param>
  /// <param name="documents">The assembly that embeds the documents, under <see cref="DocumentationCatalog.ResourcePrefix" />.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  internal DocumentationLibrary(IStringLocalizer<Common> localizer, Assembly documents)
  {
    ArgumentNullException.ThrowIfNull(localizer);
    ArgumentNullException.ThrowIfNull(documents);

    _localizer = localizer;
    _documents = documents;
  }

  #endregion

  #region Properties

  /// <summary>Gets the deployed version, the tag of the documentation, without the leading <c>v</c> nor the build metadata.</summary>
  public static string Version { get; } = VersionOf(typeof(DocumentationLibrary).Assembly);

  #endregion

  #region Methods

  /// <summary>Gets the name of the embedded resource of a document.</summary>
  /// <param name="slug">The slug of the document.</param>
  /// <param name="language">The language, <c>en</c> for the document itself.</param>
  /// <returns>The name of the resource.</returns>
  internal static string ResourceName(string slug, string language) =>
    language == ConsoleLanguages.DefaultCode
      ? $"{DocumentationCatalog.ResourcePrefix}{slug}.md"
      : $"{DocumentationCatalog.ResourcePrefix}{slug}.{language}.md";

  /// <summary>Gets a published document, rendered for the language of the console of the current request.</summary>
  /// <param name="entry">The published document.</param>
  /// <param name="cancellationToken">The token that stops the wait (the rendering itself goes on for the next caller).</param>
  /// <returns>The rendered document, translated when the translation exists, else in English.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="entry" /> is <see langword="null" />.</exception>
  public Task<RenderedDocument> GetAsync(DocumentEntry entry, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(entry);

    var requested = ConsoleLanguages.Current.Code;
    var served = requested != ConsoleLanguages.DefaultCode && Exists(ResourceName(entry.Slug, requested)) ? requested : ConsoleLanguages.DefaultCode;

    // The texts are read now, in the culture of the request: the rendering may run on another thread.
    var labels = Labels(served);
    var lazy = _rendered.GetOrAdd($"{entry.Slug}|{requested}", _ => new Lazy<Task<RenderedDocument>>(() => RenderAsync(entry, served, requested, labels)));

    return lazy.Value.WaitAsync(cancellationToken);
  }

  /// <summary>Gets the title of every published document, in the order of the navigation.</summary>
  /// <param name="cancellationToken">The token that stops the wait.</param>
  /// <returns>The title of each document, by slug, in the language served for each.</returns>
  public async Task<IReadOnlyDictionary<string, string>> GetTitlesAsync(CancellationToken cancellationToken)
  {
    var titles = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var entry in DocumentationCatalog.Entries)
    {
      titles[entry.Slug] = (await GetAsync(entry, cancellationToken).ConfigureAwait(false)).Title;
    }

    return titles;
  }

  private static string VersionOf(Assembly assembly)
  {
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    var metadata = version.IndexOf('+', StringComparison.Ordinal);

    return metadata < 0 ? version : version[..metadata];
  }

  /// <summary>Tells whether the assembly embeds a resource.</summary>
  /// <param name="name">The name of the resource.</param>
  /// <returns><see langword="true" /> when it is embedded.</returns>
  private bool Exists(string name) => _documents.GetManifestResourceInfo(name) is not null;

  /// <summary>Reads the Markdown of an embedded document.</summary>
  /// <param name="slug">The slug of the document.</param>
  /// <param name="language">The language of the document.</param>
  /// <returns>The Markdown, or <see langword="null" /> when the build did not embed it.</returns>
  private async Task<string?> ReadAsync(string slug, string language)
  {
    var stream = _documents.GetManifestResourceStream(ResourceName(slug, language));

    if (stream is null)
    {
      return null;
    }

    await using (stream.ConfigureAwait(false))
    {
      using var reader = new StreamReader(stream);

      return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
  }

  private async Task<RenderedDocument> RenderAsync(DocumentEntry entry, string served, string requested, RenderLabels labels)
  {
    var markdown = await ReadAsync(entry.Slug, served).ConfigureAwait(false);

    // A document of the catalogue that the build did not embed renders empty: DocumentationTests catch it first.
    if (markdown is null)
    {
      return new RenderedDocument(entry, entry.Slug, string.Empty, [], served, requested);
    }

    IReadOnlyList<DocumentHeading>? anchors = null;

    if (served != ConsoleLanguages.DefaultCode && await ReadAsync(entry.Slug, ConsoleLanguages.DefaultCode).ConfigureAwait(false) is { } english)
    {
      anchors = MarkdownDocumentRenderer.Render(english, entry, ConsoleLanguages.DefaultCode, Version, labels).Headings;
    }

    return MarkdownDocumentRenderer.Render(markdown, entry, served, Version, labels, anchors) with { RequestedLanguage = requested };
  }

  /// <summary>Reads the texts of the rendering: the chrome in the language of the console, the callouts in the one of the document.</summary>
  /// <param name="language">The language of the document.</param>
  /// <returns>The texts.</returns>
  private RenderLabels Labels(string language)
  {
    var console = CultureInfo.CurrentUICulture;
    string note, tip, warning;

    try
    {
      CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
      note = _localizer["Docs_CalloutNote"];
      tip = _localizer["Docs_CalloutTip"];
      warning = _localizer["Docs_CalloutWarning"];
    }
    finally
    {
      CultureInfo.CurrentUICulture = console;
    }

    return new RenderLabels(_localizer["Docs_AnchorLabel"], _localizer["Action_Copy"], _localizer["Token_Copied"], _localizer["Token_CopyFailed"], note, tip, warning);
  }

  #endregion
}
