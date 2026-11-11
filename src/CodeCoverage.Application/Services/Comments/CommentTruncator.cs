// ------------------------------------------------------------------------------------------------
// <copyright file="CommentTruncator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Services.Comments;

/// <summary>Keeps the body of the comment under the length a forge accepts.</summary>
/// <remarks>
///   The rows of the files are given up first, the last ones first, since they are sorted by importance and the
///   comment then counts those it leaves out. If that is not enough the folded sections are given up altogether.
///   A shortened body says so, with a link to the page that shows everything.
/// </remarks>
internal static class CommentTruncator
{
  #region Fields

  /// <summary>The longest body that is published, in characters; GitHub refuses a comment over 65 536.</summary>
  public const int MaxLength = 60_000;

  #endregion

  #region Methods

  /// <summary>Finds the fullest body that fits.</summary>
  /// <param name="filesLimit">The number of files the comment may list.</param>
  /// <param name="compose">
  ///   What writes the body for a number of files; <see langword="null" /> asks for a body without any folded
  ///   section. A number below <paramref name="filesLimit" />, or <see langword="null" />, also asks for the note
  ///   that says the body was shortened.
  /// </param>
  /// <returns>The body, at most <see cref="MaxLength" /> characters long.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="compose" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="filesLimit" /> is negative.</exception>
  public static string Fit(int filesLimit, Func<int?, string> compose)
  {
    ArgumentNullException.ThrowIfNull(compose);
    ArgumentOutOfRangeException.ThrowIfNegative(filesLimit);

    var body = compose(filesLimit);

    if (body.Length <= MaxLength)
    {
      return body;
    }

    // The largest number of rows that fits: a body only grows with its rows, so the search can halve.
    string? best = null;
    var low = 0;
    var high = filesLimit - 1;

    while (low <= high)
    {
      var middle = low + (high - low) / 2;
      var candidate = compose(middle);

      if (candidate.Length <= MaxLength)
      {
        best = candidate;
        low = middle + 1;
      }
      else
      {
        high = middle - 1;
      }
    }

    if (best is not null)
    {
      return best;
    }

    var bare = compose(null);

    // Nothing is left to give up but the text itself: a title or a branch of an absurd length.
    return bare.Length <= MaxLength ? bare : bare[..MaxLength];
  }

  #endregion
}
