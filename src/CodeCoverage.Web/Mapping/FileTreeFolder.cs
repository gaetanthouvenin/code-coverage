// ------------------------------------------------------------------------------------------------
// <copyright file="FileTreeFolder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Web.Mapping;

/// <summary>A folder of the tree being built by <see cref="FileTreeBuilder" />.</summary>
/// <param name="path">The full path of the folder, empty for the root.</param>
internal sealed class FileTreeFolder(string path)
{
  #region Properties

  /// <summary>Gets the full path of the folder, empty for the root.</summary>
  public string Path { get; } = path;

  /// <summary>Gets the child folders, by name in ordinal order.</summary>
  public SortedDictionary<string, FileTreeFolder> Folders { get; } = new(StringComparer.Ordinal);

  /// <summary>Gets the files directly in the folder.</summary>
  public List<FileTotals> Files { get; } = [];

  #endregion
}
