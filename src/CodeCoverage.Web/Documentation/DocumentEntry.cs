// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Documentation;

/// <summary>A published document of the in-app documentation.</summary>
/// <param name="Slug">The last segment of its address, <c>/docs/{slug}</c>; also the name of its embedded resource.</param>
/// <param name="SourcePath">Its path in the repository, with forward slashes, e.g. <c>docs/operations.md</c>.</param>
/// <param name="Section">The section of the navigation that lists it.</param>
public sealed record DocumentEntry(string Slug, string SourcePath, DocumentationSection Section)
{
  #region Properties

  /// <summary>Gets the address of the page, relative to the base address: <c>docs/{slug}</c>.</summary>
  public string Href => DocumentationCatalog.PathPrefix + Slug;

  #endregion
}
