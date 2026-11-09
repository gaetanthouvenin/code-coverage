// ------------------------------------------------------------------------------------------------
// <copyright file="NetworkLineCounter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Uploads;

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Counts the lines of a network part as it is read, and refuses a list that is too long.</summary>
/// <param name="kind">What the part holds; the other kinds are not counted.</param>
/// <param name="maxLines">The largest number of lines.</param>
internal sealed class NetworkLineCounter(UploadPartKind kind, int maxLines)
{
  #region Fields

  /// <summary>The end of a line.</summary>
  private const byte LineFeed = (byte)'\n';

  /// <summary>The number of complete lines so far.</summary>
  private int _lines;

  /// <summary>Whether the last byte read ended a line.</summary>
  private bool _atLineStart = true;

  #endregion

  #region Methods

  /// <summary>Counts a block.</summary>
  /// <param name="block">The next bytes of the part.</param>
  /// <exception cref="UploadTooLargeException">The list exceeds its limit.</exception>
  public void Add(ReadOnlySpan<byte> block)
  {
    if (kind is not UploadPartKind.Network || block.IsEmpty)
    {
      return;
    }

    _lines += block.Count(LineFeed);
    _atLineStart = block[^1] == LineFeed;
    ThrowIfOver(_lines);
  }

  /// <summary>Counts the last line, when it has no line feed.</summary>
  /// <exception cref="UploadTooLargeException">The list exceeds its limit.</exception>
  public void Complete() => ThrowIfOver(_atLineStart ? _lines : _lines + 1);

  /// <summary>Refuses a list that is too long.</summary>
  /// <param name="lines">The number of lines.</param>
  /// <exception cref="UploadTooLargeException">The list exceeds its limit.</exception>
  private void ThrowIfOver(int lines)
  {
    if (lines > maxLines)
    {
      throw new UploadTooLargeException($"The list of files exceeds the limit of {maxLines} lines.");
    }
  }

  #endregion
}
