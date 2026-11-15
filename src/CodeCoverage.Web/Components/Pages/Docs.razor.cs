// ------------------------------------------------------------------------------------------------
// <copyright file="Docs.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The in-app documentation, <c>/docs/{slug}</c> (M4.02o): the published documents of the deployed version, with their
///   navigation, their contents and the pages before and after.
/// </summary>
/// <remarks>
///   Open without sign-in and during the first start (an open path of <c>SetupRedirectMiddleware</c>). An address outside
///   the whitelist keeps the navigation, shows an empty state and answers 404.
/// </remarks>
public partial class Docs
{
  #region Fields

  /// <summary>The number of placeholder lines of the loading state.</summary>
  private const int SkeletonLines = 6;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the slug of the page, or <see langword="null" /> for <c>/docs</c>, which opens the first page.</summary>
  [Parameter]
  public string? Slug { get; set; }

  /// <summary>Gets or sets the context of the request while the page is rendered on the server, to answer 404.</summary>
  [CascadingParameter]
  public HttpContext? HttpContext { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private DocumentationLibrary Library { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private IStringLocalizer<UiStrings> U { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the deployed version, shown in the navigation and under the title.</summary>
  private static string Version => DocumentationLibrary.Version;

  /// <summary>Gets the published documents grouped by section, in the order of the navigation.</summary>
  private static IEnumerable<IGrouping<DocumentationSection, DocumentEntry>> Sections =>
    DocumentationCatalog.Entries.GroupBy(static e => e.Section);

  /// <summary>Gets the document of the address, or <see langword="null" /> when it is not published.</summary>
  private DocumentEntry? Entry { get; set; }

  /// <summary>Gets the rendered document, or <see langword="null" /> while it loads or when the address is unknown.</summary>
  private RenderedDocument? Document { get; set; }

  /// <summary>Gets the title of each document, by slug; empty until read.</summary>
  private IReadOnlyDictionary<string, string> Titles { get; set; } = new Dictionary<string, string>();

  /// <summary>Gets a value indicating whether the navigation is unfolded, on a narrow screen.</summary>
  private bool NavOpen { get; set; }

  /// <summary>Gets the previous page in the order of the navigation.</summary>
  private DocumentEntry? Previous => Neighbour(-1);

  /// <summary>Gets the next page in the order of the navigation.</summary>
  private DocumentEntry? Next => Neighbour(1);

  /// <summary>Gets the CSS classes of the layout.</summary>
  private string LayoutClass => NavOpen ? "cc-docs cc-docs--nav-open" : "cc-docs";

  /// <summary>Gets the title of the browser tab.</summary>
  private string PageTitleText => Entry is null ? L["Nav_Docs"] : TitleOf(Entry);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override async Task OnParametersSetAsync()
  {
    NavOpen = false;
    Document = null;
    Entry = Slug is null ? DocumentationCatalog.Default : DocumentationCatalog.Find(Slug);

    if (Entry is null && HttpContext is { Response.HasStarted: false } context)
    {
      context.Response.StatusCode = StatusCodes.Status404NotFound;
    }

    Titles = await Library.GetTitlesAsync(CancellationToken.None).ConfigureAwait(true);

    if (Entry is not null)
    {
      Document = await Library.GetAsync(Entry, CancellationToken.None).ConfigureAwait(true);
    }
  }

  #endregion

  #region Methods

  private string TitleOf(DocumentEntry entry) => Titles.TryGetValue(entry.Slug, out var title) ? title : entry.Slug;

  private string SectionLabel(DocumentationSection section) => L[$"Docs_Section_{section}"];

  private DocumentEntry? Neighbour(int offset)
  {
    if (Entry is null)
    {
      return null;
    }

    var index = DocumentationCatalog.Entries.ToList().IndexOf(Entry) + offset;

    return index >= 0 && index < DocumentationCatalog.Entries.Count ? DocumentationCatalog.Entries[index] : null;
  }

  private void ToggleNav() => NavOpen = !NavOpen;

  #endregion
}
