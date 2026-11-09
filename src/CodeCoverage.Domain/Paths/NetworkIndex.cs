// ------------------------------------------------------------------------------------------------
// <copyright file="NetworkIndex.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Paths;

/// <summary>The files of the repository, as listed by the upload or read from the forge.</summary>
public sealed class NetworkIndex
{
  #region Fields

  private readonly HashSet<string> _paths;

  private readonly Dictionary<string, List<string>> _pathsByFileName;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="NetworkIndex" /> class.</summary>
  /// <param name="paths">The repository-relative paths.</param>
  /// <exception cref="ArgumentNullException"><paramref name="paths" /> is null.</exception>
  public NetworkIndex(IEnumerable<string> paths)
  {
    ArgumentNullException.ThrowIfNull(paths);

    _paths = new HashSet<string>(paths, StringComparer.Ordinal);
    _pathsByFileName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var path in _paths)
    {
      var fileName = GetFileName(path);

      if (!_pathsByFileName.TryGetValue(fileName, out var sameName))
      {
        sameName = [];
        _pathsByFileName.Add(fileName, sameName);
      }

      sameName.Add(path);
    }
  }

  #endregion

  #region Properties

  /// <summary>Gets a value indicating whether the list has no file, which is like no list at all.</summary>
  public bool IsEmpty => _paths.Count == 0;

  #endregion

  #region Methods

  /// <summary>Tests whether the repository has exactly this path.</summary>
  /// <param name="path">The repository-relative path.</param>
  /// <returns><see langword="true" /> if the path is listed, its case included.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public bool Contains(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    return _paths.Contains(path);
  }

  /// <summary>Gets the paths of the files that have a file name, whatever its case.</summary>
  /// <param name="fileName">The file name, without any directory.</param>
  /// <returns>The paths with this name, empty if there is none.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="fileName" /> is null.</exception>
  public IReadOnlyList<string> FindByFileName(string fileName)
  {
    ArgumentNullException.ThrowIfNull(fileName);

    return _pathsByFileName.TryGetValue(fileName, out var sameName) ? sameName : [];
  }

  /// <summary>Gets the last segment of a path.</summary>
  /// <param name="path">The path, with forward slashes.</param>
  /// <returns>The file name.</returns>
  internal static string GetFileName(string path) => path[(path.LastIndexOf('/') + 1)..];

  #endregion
}
