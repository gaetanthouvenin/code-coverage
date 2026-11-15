// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentLinkRewriter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>Rewrites the links of a document for the console.</summary>
/// <remarks>
///   <list type="bullet">
///     <item>A link to a published document, or to one of its headings, leads to its page: <c>docs/{slug}#{anchor}</c>.</item>
///     <item>A link to another file of the repository leads to GitHub, at the tag of the deployed version.</item>
///     <item>An anchor of the same document names its page, since the base address of the console is <c>/</c>.</item>
///     <item><c>http</c>, <c>https</c> and <c>mailto</c> links are kept; any other scheme (<c>javascript:</c>…) loses its link.</item>
///   </list>
/// </remarks>
internal static partial class DocumentLinkRewriter
{
  #region Fields

  /// <summary>
  ///   The GitHub repository the deployed images are built from, where the files that are not published are read. No
  ///   setting carries it, as for the action and the template of <c>UploadSetup</c>.
  /// </summary>
  public const string Repository = "gaetanthouvenin/code-coverage";

  /// <summary>The class of a link that leaves the console.</summary>
  private const string ExternalClass = "cc-md__ext";

  #endregion

  #region Methods

  /// <summary>Rewrites every link of a parsed document.</summary>
  /// <param name="document">The parsed document.</param>
  /// <param name="entry">The published document it comes from, which relative links start from.</param>
  /// <param name="version">The deployed version, without the leading <c>v</c>.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static void Rewrite(MarkdownDocument document, DocumentEntry entry, string version)
  {
    ArgumentNullException.ThrowIfNull(document);
    ArgumentNullException.ThrowIfNull(entry);
    ArgumentNullException.ThrowIfNull(version);

    foreach (var link in document.Descendants<LinkInline>().ToList())
    {
      if (Resolve(link.Url, entry, version) is not { } target)
      {
        Unwrap(link);
        continue;
      }

      link.Url = target.Href;

      if (target.External)
      {
        link.GetAttributes().AddClass(ExternalClass);
      }
    }

    foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
    {
      if (autolink.IsEmail)
      {
        continue;
      }

      if (Resolve(autolink.Url, entry, version) is { External: true })
      {
        autolink.GetAttributes().AddClass(ExternalClass);
      }
      else
      {
        autolink.ReplaceBy(new LiteralInline(autolink.Url));
      }
    }
  }

  /// <summary>Resolves the address of a link of a document.</summary>
  /// <param name="url">The address written in the Markdown.</param>
  /// <param name="entry">The published document it comes from.</param>
  /// <param name="version">The deployed version, without the leading <c>v</c>.</param>
  /// <returns>The address to render and whether it leaves the console, or <see langword="null" /> to drop the link.</returns>
  internal static (string Href, bool External)? Resolve(string? url, DocumentEntry entry, string version)
  {
    ArgumentNullException.ThrowIfNull(entry);
    ArgumentNullException.ThrowIfNull(version);

    if (string.IsNullOrWhiteSpace(url))
    {
      return null;
    }

    url = url.Trim();

    if (url.StartsWith('#'))
    {
      return (entry.Href + url, false);
    }

    if (Scheme().Match(url) is { Success: true } scheme)
    {
      return scheme.Groups["name"].Value.ToLowerInvariant() switch
      {
        "http" or "https" => (url, true),
        "mailto" => (url, false),
        var _ => null,
      };
    }

    if (url.StartsWith("//", StringComparison.Ordinal) || url.Contains('\\', StringComparison.Ordinal))
    {
      return null;
    }

    var hash = url.IndexOf('#', StringComparison.Ordinal);
    var fragment = hash < 0 ? string.Empty : url[hash..];
    var path = hash < 0 ? url : url[..hash];
    var query = path.IndexOf('?', StringComparison.Ordinal);

    path = query < 0 ? path : path[..query];

    if (Normalize(entry.SourcePath, path) is not { } repositoryPath)
    {
      return null;
    }

    if (DocumentationCatalog.FindBySource(repositoryPath) is { } published)
    {
      return (published.Href + fragment, false);
    }

    var escaped = string.Join('/', repositoryPath.Split('/').Select(Uri.EscapeDataString));

    return ($"https://github.com/{Repository}/blob/v{version}/{escaped}{fragment}", true);
  }

  /// <summary>Resolves a relative path of a document into a path of the repository.</summary>
  /// <param name="sourcePath">The path of the document in the repository.</param>
  /// <param name="relative">The path written in the link; a leading slash starts from the root of the repository.</param>
  /// <returns>The path in the repository, without trailing slash, or <see langword="null" /> when it leaves the repository.</returns>
  private static string? Normalize(string sourcePath, string relative)
  {
    var segments = new List<string>();

    if (!relative.StartsWith('/'))
    {
      segments.AddRange(sourcePath.Split('/')[..^1]);
    }

    foreach (var segment in Uri.UnescapeDataString(relative).Split('/', StringSplitOptions.RemoveEmptyEntries))
    {
      switch (segment)
      {
        case ".":
          continue;
        case "..":
          if (segments.Count == 0)
          {
            return null;
          }

          segments.RemoveAt(segments.Count - 1);
          continue;
        default:
          segments.Add(segment);
          continue;
      }
    }

    return string.Join('/', segments);
  }

  /// <summary>Replaces a link by its text.</summary>
  /// <param name="link">The link.</param>
  private static void Unwrap(LinkInline link)
  {
    var child = link.FirstChild;

    while (child is not null)
    {
      var next = child.NextSibling;

      child.Remove();
      link.InsertBefore(child);
      child = next;
    }

    link.Remove();
  }

  [GeneratedRegex("^(?<name>[a-zA-Z][a-zA-Z0-9+.-]*):")]
  private static partial Regex Scheme();

  #endregion
}
