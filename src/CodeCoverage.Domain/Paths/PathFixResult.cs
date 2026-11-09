// ------------------------------------------------------------------------------------------------
// <copyright file="PathFixResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Paths;

/// <summary>The outcome of fixing one path of a report.</summary>
public sealed record PathFixResult
{
  #region Ctors

  private PathFixResult(PathFixStatus status, string? path)
  {
    Status = status;
    Path = path;
  }

  #endregion

  #region Properties

  /// <summary>Gets the result of a path that matches no file of the repository.</summary>
  public static PathFixResult Unresolved { get; } = new(PathFixStatus.Unresolved, null);

  /// <summary>Gets the result of a path that matches several files equally well.</summary>
  public static PathFixResult Ambiguous { get; } = new(PathFixStatus.Ambiguous, null);

  /// <summary>Gets what was concluded about the path.</summary>
  public PathFixStatus Status { get; }

  /// <summary>Gets the repository-relative path, or <see langword="null" /> if it is dropped.</summary>
  public string? Path { get; }

  #endregion

  #region Methods

  /// <summary>Creates the result of a path retained for a file of the repository.</summary>
  /// <param name="path">The repository-relative path.</param>
  /// <returns>The result.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public static PathFixResult Resolved(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    return new PathFixResult(PathFixStatus.Resolved, path);
  }

  /// <summary>Creates the result of a path kept without a list of files to confirm it.</summary>
  /// <param name="path">The repository-relative path.</param>
  /// <returns>The result.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public static PathFixResult Unverified(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    return new PathFixResult(PathFixStatus.Unverified, path);
  }

  #endregion
}
