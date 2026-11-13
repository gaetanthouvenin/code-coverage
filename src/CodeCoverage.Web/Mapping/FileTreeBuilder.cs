// ------------------------------------------------------------------------------------------------
// <copyright file="FileTreeBuilder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Mapping;

/// <summary>
///   Builds the tree of the files of a commit from their flat list: a folder sums the hits, misses and partials of its
///   files, and a chain of folders with a single child folder is shown as one node (<c>src/Billing.Domain</c>).
/// </summary>
internal static class FileTreeBuilder
{
  #region Methods

  /// <summary>Builds the tree.</summary>
  /// <param name="files">The files, in any order.</param>
  /// <param name="precision">The number of decimals of a coverage.</param>
  /// <param name="rounding">How a coverage is rounded.</param>
  /// <returns>The roots and the flat list of the files, both in ordinal path order.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="files" /> is <see langword="null" />.</exception>
  public static FileTreeViewModel Build(IReadOnlyList<FileTotals> files, int precision, RoundingMode rounding)
  {
    ArgumentNullException.ThrowIfNull(files);

    var root = new FileTreeFolder(string.Empty);

    foreach (var file in files.OrderBy(static f => f.Path, StringComparer.Ordinal))
    {
      var segments = file.Path.Split('/');
      var folder = root;

      for (var i = 0; i < segments.Length - 1; i++)
      {
        var path = string.Join('/', segments, 0, i + 1);

        if (!folder.Folders.TryGetValue(segments[i], out var child))
        {
          child = new FileTreeFolder(path);
          folder.Folders.Add(segments[i], child);
        }

        folder = child;
      }

      folder.Files.Add(file);
    }

    var roots = Children(root, precision, rounding);
    var flat = new List<FileNodeViewModel>();
    Collect(roots, flat);

    return new FileTreeViewModel(roots, flat);
  }

  private static IReadOnlyList<FileNodeViewModel> Children(FileTreeFolder folder, int precision, RoundingMode rounding)
  {
    var folders = folder.Folders.Values.Select(f => ToNode(f, precision, rounding));
    var files = folder.Files.Select(f => ToNode(f.Path, f.Path[(f.Path.LastIndexOf('/') + 1)..], false, f.Totals, [], precision, rounding));

    return [.. folders.Concat(files).OrderBy(static n => n.Path, StringComparer.Ordinal)];
  }

  // A folder whose only child is a folder is merged with it: « src » holding only « Billing.Domain » shows once.
  private static FileNodeViewModel ToNode(FileTreeFolder folder, int precision, RoundingMode rounding)
  {
    var name = folder.Path[(folder.Path.LastIndexOf('/') + 1)..];
    var current = folder;

    while (current is { Files.Count: 0, Folders.Count: 1 })
    {
      current = current.Folders.Values.Single();
      name = $"{name}/{current.Path[(current.Path.LastIndexOf('/') + 1)..]}";
    }

    var children = Children(current, precision, rounding);
    var totals = new CoverageTotals(children.Sum(static c => c.Hits), children.Sum(static c => c.Misses), children.Sum(static c => c.Partials), 0, 0);

    return ToNode(current.Path, name, true, totals, children, precision, rounding);
  }

  private static FileNodeViewModel ToNode(
    string path,
    string name,
    bool isFolder,
    CoverageTotals totals,
    IReadOnlyList<FileNodeViewModel> children,
    int precision,
    RoundingMode rounding) =>
    new(
      path,
      name,
      isFolder,
      totals.Lines.ToString("N0", CultureInfo.CurrentCulture),
      totals.Hits,
      totals.Partials,
      totals.Misses,
      totals.Coverage,
      DisplayFormatter.Percent(totals.Coverage, precision, rounding),
      children
    );

  private static void Collect(IReadOnlyList<FileNodeViewModel> nodes, List<FileNodeViewModel> files)
  {
    foreach (var node in nodes)
    {
      if (node.IsFolder)
      {
        Collect(node.Children, files);
      }
      else
      {
        files.Add(node);
      }
    }
  }

  #endregion
}
