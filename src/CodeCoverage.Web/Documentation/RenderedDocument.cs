// ------------------------------------------------------------------------------------------------
// <copyright file="RenderedDocument.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Documentation;

/// <summary>A published document rendered to HTML.</summary>
/// <param name="Entry">The document of the whitelist.</param>
/// <param name="Title">The text of its first-level heading, shown as the title of the page.</param>
/// <param name="Html">Its content without that heading: the Markdown rendered on the server, raw HTML neutralised.</param>
/// <param name="Headings">Its headings of level 2 to 6, in the order of the document.</param>
/// <param name="Language">The language of the content: <c>fr</c> for a translation, <c>en</c> otherwise.</param>
public sealed record RenderedDocument(DocumentEntry Entry, string Title, string Html, IReadOnlyList<DocumentHeading> Headings, string Language)
{
  #region Properties

  /// <summary>Gets the headings of level 2, the contents of the page (« On this page »).</summary>
  public IReadOnlyList<DocumentHeading> Contents => [.. Headings.Where(static h => h.Level == 2)];

  #endregion

  #region Methods

  /// <summary>Tells whether the document has a heading with this identifier.</summary>
  /// <param name="id">The identifier of the heading.</param>
  /// <returns><see langword="true" /> when an anchor <c>#{id}</c> leads into the document.</returns>
  public bool HasAnchor(string id) => Headings.Any(h => string.Equals(h.Id, id, StringComparison.Ordinal));

  #endregion
}
