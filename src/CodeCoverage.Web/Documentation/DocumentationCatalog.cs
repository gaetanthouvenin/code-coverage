// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationCatalog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Documentation;

/// <summary>The whitelist of the documents served by <c>/docs</c> (M4.02o), in the order of the navigation.</summary>
/// <remarks>
///   <para>
///     One page per document. The documents are embedded in the Web at build (<c>PublishedDocument</c> items of
///     <c>CodeCoverage.Web.csproj</c>, with the same slugs): the documentation shown is the one of the deployed version.
///     The plan, the ADRs, <c>ARCHITECTURE.md</c> and <c>CONVENTIONS.md</c> are never published.
///   </para>
///   <para>
///     The mockup of M4.02n split the documents into thirteen pages with headings of its own; the owner chose one page
///     per existing document, with its real headings, instead (2026-11-15).
///   </para>
/// </remarks>
public static class DocumentationCatalog
{
  #region Fields

  /// <summary>The first segment of the address of a page, with its trailing slash.</summary>
  public const string PathPrefix = "docs/";

  /// <summary>The prefix of the name of an embedded document.</summary>
  internal const string ResourcePrefix = "CodeCoverage.Web.Docs.";

  #endregion

  #region Properties

  /// <summary>Gets the published documents, in the order of the navigation, which is also the order of the pager.</summary>
  public static IReadOnlyList<DocumentEntry> Entries { get; } =
  [
    new("operations", "docs/operations.md", DocumentationSection.GettingStarted),
    new("organization-onboarding", "docs/operations/organization-onboarding.md", DocumentationSection.GitHubApp),
    new("github-checklist", "docs/operations/github-verification-checklist.md", DocumentationSection.GitHubApp),
    new("github-actions", "action/README.md", DocumentationSection.ConfigureRepository),
    new("deployment-azure", "docs/deployment-azure.md", DocumentationSection.Operations),
    new("migration-from-codecov", "docs/migration-from-codecov.md", DocumentationSection.Migration),
  ];

  /// <summary>Gets the page that <c>/docs</c> opens.</summary>
  public static DocumentEntry Default => Entries[0];

  #endregion

  #region Methods

  /// <summary>Finds a published document by the slug of its address.</summary>
  /// <param name="slug">The slug, compared without regard to case; <see langword="null" /> finds nothing.</param>
  /// <returns>The document, or <see langword="null" /> when it is not published.</returns>
  public static DocumentEntry? Find(string? slug) =>
    string.IsNullOrEmpty(slug) ? null : Entries.FirstOrDefault(e => string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase));

  /// <summary>Finds a published document by its path in the repository.</summary>
  /// <param name="sourcePath">The path, with forward slashes and no leading slash.</param>
  /// <returns>The document, or <see langword="null" /> when that file is not published.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sourcePath" /> is <see langword="null" />.</exception>
  public static DocumentEntry? FindBySource(string sourcePath)
  {
    ArgumentNullException.ThrowIfNull(sourcePath);

    return Entries.FirstOrDefault(e => string.Equals(e.SourcePath, sourcePath, StringComparison.Ordinal));
  }

  #endregion
}
