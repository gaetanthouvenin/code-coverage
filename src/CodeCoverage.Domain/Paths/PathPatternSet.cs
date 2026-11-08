// ------------------------------------------------------------------------------------------------
// <copyright file="PathPatternSet.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Paths;

/// <summary>A list of patterns that decides together whether a value is included.</summary>
public sealed class PathPatternSet
{
  #region Fields

  private readonly PathPattern[] _positives;

  private readonly PathPattern[] _negatives;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="PathPatternSet" /> class.</summary>
  /// <param name="patterns">The patterns, whose order does not matter.</param>
  /// <exception cref="ArgumentNullException"><paramref name="patterns" /> is null.</exception>
  public PathPatternSet(IEnumerable<PathPattern> patterns)
  {
    ArgumentNullException.ThrowIfNull(patterns);

    PathPattern[] all = [.. patterns];

    Patterns = all;
    _positives = Array.FindAll(all, pattern => !pattern.IsNegative);
    _negatives = Array.FindAll(all, pattern => pattern.IsNegative);
  }

  #endregion

  #region Properties

  /// <summary>Gets the patterns of the set.</summary>
  public IReadOnlyList<PathPattern> Patterns { get; }

  #endregion

  #region Methods

  /// <summary>Tests whether the set includes a value.</summary>
  /// <param name="value">The repository-relative path, or the branch name.</param>
  /// <returns>
  ///   <see langword="true" /> if no negative pattern matches and if a positive pattern matches or
  ///   there is no positive pattern; otherwise, <see langword="false" />.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="value" /> is null.</exception>
  public bool Includes(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    return !Array.Exists(_negatives, pattern => pattern.Match(value))
      && (_positives.Length == 0 || Array.Exists(_positives, pattern => pattern.Match(value)));
  }

  #endregion
}
