// ------------------------------------------------------------------------------------------------
// <copyright file="GzipDetector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Recognizes the content that is already compressed with gzip.</summary>
internal static class GzipDetector
{
  #region Fields

  /// <summary>The number of bytes to read to recognize gzip.</summary>
  public const int HeaderLength = 2;

  /// <summary>The first byte of the signature of gzip.</summary>
  private const byte First = 0x1F;

  /// <summary>The second byte of the signature of gzip.</summary>
  private const byte Second = 0x8B;

  #endregion

  #region Methods

  /// <summary>Tells whether a content starts with the signature of gzip.</summary>
  /// <param name="head">The first bytes of the content.</param>
  /// <returns><see langword="true" /> when it is gzip.</returns>
  public static bool IsGzip(ReadOnlySpan<byte> head) => head.Length >= HeaderLength && head[0] == First && head[1] == Second;

  #endregion
}
