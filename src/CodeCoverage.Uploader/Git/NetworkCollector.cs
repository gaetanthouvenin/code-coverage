// ------------------------------------------------------------------------------------------------
// <copyright file="NetworkCollector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Git;

/// <summary>Lists the files of the repository, sent as the network of the upload.</summary>
/// <remarks>
///   <c>-z</c> gives the paths as they are, without quoting, and <c>ls-files</c> only reads the index: it works in a clone
///   of depth 1.
/// </remarks>
internal static class NetworkCollector
{
  #region Fields

  private const char Separator = '\0';

  #endregion

  #region Methods

  /// <summary>Lists the files.</summary>
  /// <param name="gitRaw">Runs git; returns its raw output, or <see langword="null" /> on a failure.</param>
  /// <returns>The paths; empty when git failed.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="gitRaw" /> is <see langword="null" />.</exception>
  public static IReadOnlyList<string> Collect(Func<string[], string?> gitRaw)
  {
    ArgumentNullException.ThrowIfNull(gitRaw);

    return gitRaw(["ls-files", "-z"])?.Split(Separator, StringSplitOptions.RemoveEmptyEntries) ?? [];
  }

  #endregion
}
