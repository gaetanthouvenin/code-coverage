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
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Documentation;

/// <summary>Reads and renders the published documents embedded in the Web, once per document and language.</summary>
/// <param name="localizer">The texts of the console, read in the culture of the request.</param>
/// <remarks>
///   The documents are in English. A translation <c>{name}.fr.md</c> next to a document is served when the console speaks
///   French. The rendered documents are kept for the life of the host: the embedded documents never change.
/// </remarks>
public sealed class DocumentationLibrary(IStringLocalizer<Common> localizer)
{
  #region Fields

  /// <summary>The language of the documents.</summary>
  private const string English = "en";

  /// <summary>The language of the translations.</summary>
  private const string French = "fr";

  /// <summary>The rendered documents, by slug and language of the console.</summary>
  private readonly ConcurrentDictionary<string, Lazy<Task<RenderedDocument>>> _documents = new(StringComparer.Ordinal);

  #endregion

  #region Properties

  /// <summary>Gets the deployed version, the tag of the documentation, without the leading <c>v</c> nor the build metadata.</summary>
  public static string Version { get; } = VersionOf(typeof(DocumentationLibrary).Assembly);

  #endregion

  #region Methods

  /// <summary>Gets a published document, rendered for the culture of the current request.</summary>
  /// <param name="entry">The published document.</param>
  /// <param name="cancellationToken">The token that stops the wait (the rendering itself goes on for the next caller).</param>
  /// <returns>The rendered document.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="entry" /> is <see langword="null" />.</exception>
  public Task<RenderedDocument> GetAsync(DocumentEntry entry, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(entry);

    var console = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == French ? French : English;
    var language = console == French && Exists(ResourceName(entry, French)) ? French : English;

    // The texts are read now, in the culture of the request: the rendering may run on another thread.
    var labels = Labels(language);
    var lazy = _documents.GetOrAdd($"{entry.Slug}|{console}", _ => new Lazy<Task<RenderedDocument>>(() => RenderAsync(entry, language, labels)));

    return lazy.Value.WaitAsync(cancellationToken);
  }

  /// <summary>Gets the title of every published document, in the order of the navigation.</summary>
  /// <param name="cancellationToken">The token that stops the wait.</param>
  /// <returns>The title of each document, by slug.</returns>
  public async Task<IReadOnlyDictionary<string, string>> GetTitlesAsync(CancellationToken cancellationToken)
  {
    var titles = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var entry in DocumentationCatalog.Entries)
    {
      titles[entry.Slug] = (await GetAsync(entry, cancellationToken).ConfigureAwait(false)).Title;
    }

    return titles;
  }

  /// <summary>Gets the name of the embedded resource of a document.</summary>
  /// <param name="entry">The published document.</param>
  /// <param name="language">The language, <c>en</c> for the document itself.</param>
  /// <returns>The name of the resource.</returns>
  private static string ResourceName(DocumentEntry entry, string language) =>
    language == English ? $"{DocumentationCatalog.ResourcePrefix}{entry.Slug}.md" : $"{DocumentationCatalog.ResourcePrefix}{entry.Slug}.{language}.md";

  /// <summary>Tells whether the Web embeds a resource.</summary>
  /// <param name="name">The name of the resource.</param>
  /// <returns><see langword="true" /> when it is embedded.</returns>
  private static bool Exists(string name) =>
    typeof(DocumentationLibrary).Assembly.GetManifestResourceInfo(name) is not null;

  private static async Task<RenderedDocument> RenderAsync(DocumentEntry entry, string language, RenderLabels labels)
  {
    var stream = typeof(DocumentationLibrary).Assembly.GetManifestResourceStream(ResourceName(entry, language));

    // A document of the catalogue that the build did not embed renders empty: DocumentationTests catch it first.
    if (stream is null)
    {
      return new RenderedDocument(entry, entry.Slug, string.Empty, [], language);
    }

    string markdown;

    await using (stream.ConfigureAwait(false))
    {
      using var reader = new StreamReader(stream);

      markdown = await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    return MarkdownDocumentRenderer.Render(markdown, entry, language, Version, labels);
  }

  private static string VersionOf(Assembly assembly)
  {
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    var metadata = version.IndexOf('+', StringComparison.Ordinal);

    return metadata < 0 ? version : version[..metadata];
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
      note = localizer["Docs_CalloutNote"];
      tip = localizer["Docs_CalloutTip"];
      warning = localizer["Docs_CalloutWarning"];
    }
    finally
    {
      CultureInfo.CurrentUICulture = console;
    }

    return new RenderLabels(localizer["Docs_AnchorLabel"], localizer["Action_Copy"], localizer["Token_Copied"], localizer["Token_CopyFailed"], note, tip, warning);
  }

  #endregion
}
